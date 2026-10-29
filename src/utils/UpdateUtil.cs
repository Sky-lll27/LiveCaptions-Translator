// zh-CN fork: 本文件由 Sky-lll27 修改（2026-10），更新检查改为指向本汉化仓库，
// 并改为「网页跳转优先、API 兜底」的双通道；版本比较由「字符串不等」改为「按版本号逐段比较」。
// Based on SakiRinn/LiveCaptions-Translator (Apache-2.0). Modified per §4(b).

using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LiveCaptionsTranslator.utils
{
    public static class UpdateUtil
    {
        // 本汉化版：更新检查指向自己的仓库，避免把用户引导去下载无汉化、无「文本」页的上游版本。
        public const string GitHubRepoUrl = "https://github.com/Sky-lll27/LiveCaptions-Translator";
        public const string GitHubReleasesUrl = "https://github.com/Sky-lll27/LiveCaptions-Translator/releases";
        public const string GitHubLatestReleaseApi = "https://api.github.com/repos/Sky-lll27/LiveCaptions-Translator/releases/latest";

        /// <summary>
        /// releases 页面地址。访问它会 302 跳转到 /releases/tag/&lt;tag&gt;，
        /// 从跳转后的地址里就能读出最新版本号，**不消耗 GitHub API 配额**。
        /// 相比 API 的优势：api.github.com 对未登录请求限制每 IP 每小时 60 次，
        /// 而共用代理出口时这点配额很容易被别人用光（实测撞过 403）；
        /// 网页通道没有这个限制。
        /// </summary>
        public const string GitHubLatestReleasePage = "https://github.com/Sky-lll27/LiveCaptions-Translator/releases/latest";

        /// <summary>从 /releases/tag/v1.7.0 这样的地址里取出 tag。</summary>
        private static readonly Regex releaseTagInUrl = new(@"/releases/tag/([^/?#]+)", RegexOptions.Compiled);

        /// <summary>更新检查的结果类别。用它决定给用户看什么话，而不是笼统报一句错。</summary>
        public enum CheckStatus
        {
            /// <summary>检查成功，拿到了远端最新版本号。</summary>
            Success,
            /// <summary>GitHub API 限流（未登录每小时 60 次），且网页通道也没拿到。</summary>
            RateLimited,
            /// <summary>连不上（代理未开 / 断网 / DNS / TLS 失败）。</summary>
            NetworkError,
            /// <summary>连上了但拿不到有效版本号（仓库还没有发行版等）。</summary>
            NoRelease,
            /// <summary>其它未预期的错误。</summary>
            Unknown,
        }

        public sealed class CheckResult
        {
            public CheckStatus Status { get; init; }
            /// <summary>成功时是远端版本号；失败时为空。</summary>
            public string LatestVersion { get; init; } = string.Empty;
            /// <summary>失败时的技术细节，供用户反馈问题时提供。</summary>
            public string Detail { get; init; } = string.Empty;
            /// <summary>这次结果是从哪条通道得到的，便于排查（web / api）。</summary>
            public string Source { get; init; } = string.Empty;
        }

        /// <summary>
        /// 检查远端最新版本。**不抛异常**：把失败原因归类后返回，
        /// 由调用方决定怎么告诉用户（而不是一句 [ERROR] 了事）。
        ///
        /// 策略：先走网页通道（无配额限制），失败再退回 API 通道。
        /// 两条都失败时，优先把 API 的「限流」结论暴露给用户 —— 那是最需要说明的情况。
        /// </summary>
        public static async Task<CheckResult> CheckLatestAsync(CancellationToken token = default)
        {
            // 1) 网页通道：不消耗 API 配额，绝大多数情况够用。
            var webResult = await CheckViaWebAsync(token);
            if (webResult.Status == CheckStatus.Success)
                return webResult;

            token.ThrowIfCancellationRequested();

            // 2) API 通道：网页通道失败时兜底（也用于识别限流这类明确原因）。
            var apiResult = await CheckViaApiAsync(token);
            if (apiResult.Status == CheckStatus.Success)
                return apiResult;

            // 3) 两条都失败。优先报告更有信息量的那一条：
            //    API 明确说是限流 → 报限流；否则报网络/未知原因。
            if (apiResult.Status == CheckStatus.RateLimited)
            {
                return new CheckResult
                {
                    Status = CheckStatus.RateLimited,
                    Detail = $"网页通道：{webResult.Detail}；API 通道：{apiResult.Detail}",
                    Source = "api",
                };
            }

            // 网页通道若能连上但拿不到版本（例如还没有发行版），比「网络错误」更准确。
            if (webResult.Status == CheckStatus.NoRelease)
            {
                return new CheckResult
                {
                    Status = CheckStatus.NoRelease,
                    Detail = webResult.Detail,
                    Source = "web",
                };
            }

            // 其余情况：如果网页通道是网络错误，就用它（说明根本连不上）。
            return new CheckResult
            {
                Status = webResult.Status == CheckStatus.NetworkError
                    ? CheckStatus.NetworkError
                    : CheckStatus.Unknown,
                Detail = $"网页通道：{webResult.Detail}；API 通道：{apiResult.Detail}",
                Source = "web",
            };
        }

        /// <summary>
        /// 网页通道：请求 releases/latest，让它自动跳转到 releases/tag/&lt;tag&gt;，
        /// 再从最终地址里解析出版本号。不消耗 API 配额，也不需要任何凭据。
        /// </summary>
        private static async Task<CheckResult> CheckViaWebAsync(CancellationToken token)
        {
            try
            {
                // 注意：这里**不要**自己 new HttpClientHandler 再设 Proxy。
                // 实测（本机 FlClash 开在 127.0.0.1:7890）：
                //   · 默认构造（走系统代理）→ 2.3 秒拿到 tag
                //   · new HttpClientHandler()        → 10 秒超时
                //   · 显式指定 WebProxy             → 10 秒超时
                // 用户开代理时依赖的是系统代理设置，所以必须用默认行为。
                using var client = new HttpClient()
                {
                    Timeout = TimeSpan.FromSeconds(10)
                };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("LiveCaptions-Translator");

                // AllowAutoRedirect 默认为 true，会跟随 302 到 /releases/tag/<tag>，
                // 因此能读到最终地址里的版本号。
                using var response = await client.GetAsync(GitHubLatestReleasePage, token);

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return new CheckResult { Status = CheckStatus.NoRelease, Detail = "网页 HTTP 404" };

                if (!response.IsSuccessStatusCode)
                    return new CheckResult
                    {
                        Status = CheckStatus.Unknown,
                        Detail = $"网页 HTTP {(int)response.StatusCode}",
                    };

                // 跳转后如果仍停在 /releases/latest，说明仓库还没有发行版。
                string finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? string.Empty;
                var match = releaseTagInUrl.Match(finalUrl);
                if (!match.Success)
                    return new CheckResult { Status = CheckStatus.NoRelease, Detail = "网页未跳转到具体 tag" };

                string rawTag = Uri.UnescapeDataString(match.Groups[1].Value);
                string version = RegexPatterns.VersionNumber().Replace(rawTag, "");
                if (string.IsNullOrEmpty(version))
                    return new CheckResult { Status = CheckStatus.NoRelease, Detail = $"tag 无法解析：{rawTag}" };

                return new CheckResult
                {
                    Status = CheckStatus.Success,
                    LatestVersion = version,
                    Detail = rawTag,
                    Source = "web",
                };
            }
            // 注意顺序：TaskCanceledException 继承自 OperationCanceledException，
            // 必须先捕获更具体的类型；超时在 .NET 里表现为 TaskCanceledException。
            catch (TaskCanceledException)
            {
                return new CheckResult { Status = CheckStatus.NetworkError, Detail = "网页请求超时" };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                return new CheckResult
                {
                    Status = CheckStatus.NetworkError,
                    Detail = ex.InnerException?.Message ?? ex.Message,
                };
            }
            catch (Exception ex)
            {
                return new CheckResult { Status = CheckStatus.Unknown, Detail = ex.Message };
            }
        }

        /// <summary>API 通道：读 api.github.com 的 JSON。受未登录配额限制（60 次/小时/IP）。</summary>
        private static async Task<CheckResult> CheckViaApiAsync(CancellationToken token)
        {
            try
            {
                using var client = new HttpClient()
                {
                    Timeout = TimeSpan.FromSeconds(8)
                };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("LiveCaptions-Translator");
                // 指定 GitHub API 版本，避免将来行为漂移。
                client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

                using var response = await client.GetAsync(GitHubLatestReleaseApi, token);

                if (response.StatusCode == HttpStatusCode.Forbidden ||
                    response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    // GitHub 对未登录请求限流，超了就是 403/429 + rate limit exceeded。
                    return new CheckResult { Status = CheckStatus.RateLimited, Detail = $"API HTTP {(int)response.StatusCode}" };
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return new CheckResult { Status = CheckStatus.NoRelease, Detail = "API HTTP 404" };

                if (!response.IsSuccessStatusCode)
                    return new CheckResult
                    {
                        Status = CheckStatus.Unknown,
                        Detail = $"API HTTP {(int)response.StatusCode}",
                    };

                string body = await response.Content.ReadAsStringAsync(token);
                using var doc = JsonDocument.Parse(body);
                var latestVersionRaw = doc.RootElement.GetProperty("tag_name").GetString();
                var latestVersion = string.IsNullOrEmpty(latestVersionRaw)
                    ? String.Empty
                    : RegexPatterns.VersionNumber().Replace(latestVersionRaw, "");

                if (string.IsNullOrEmpty(latestVersion))
                    return new CheckResult { Status = CheckStatus.NoRelease, Detail = "tag_name 为空" };

                return new CheckResult
                {
                    Status = CheckStatus.Success,
                    LatestVersion = latestVersion,
                    Detail = latestVersionRaw ?? string.Empty,
                    Source = "api",
                };
            }
            catch (TaskCanceledException)
            {
                return new CheckResult { Status = CheckStatus.NetworkError, Detail = "API 请求超时" };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                return new CheckResult
                {
                    Status = CheckStatus.NetworkError,
                    Detail = ex.InnerException?.Message ?? ex.Message,
                };
            }
            catch (JsonException ex)
            {
                return new CheckResult { Status = CheckStatus.Unknown, Detail = "返回内容无法解析：" + ex.Message };
            }
            catch (Exception ex)
            {
                return new CheckResult { Status = CheckStatus.Unknown, Detail = ex.Message };
            }
        }

        public static async Task<string> GetLatestVersion()
        {
            var result = await CheckLatestAsync();
            return result.Status == CheckStatus.Success ? result.LatestVersion : string.Empty;
        }

        /// <summary>
        /// 判断 <paramref name="latest"/> 是否比 <paramref name="current"/> 新。
        /// 只有「确实更新」才提示，避免装了更新版本仍然反复弹窗。
        /// 任一侧无法解析成数字版本时返回 false（宁可不说，也不误报）。
        /// </summary>
        public static bool IsNewerVersion(string? latest, string? current)
        {
            if (string.IsNullOrEmpty(latest) || string.IsNullOrEmpty(current))
                return false;

            string[] latestParts = latest.Split('.');
            string[] currentParts = current.Split('.');

            int length = Math.Max(latestParts.Length, currentParts.Length);
            for (int i = 0; i < length; i++)
            {
                if (!int.TryParse(i < latestParts.Length ? latestParts[i] : "0", out int l) ||
                    !int.TryParse(i < currentParts.Length ? currentParts[i] : "0", out int c))
                    return false;

                if (l > c)
                    return true;
                if (l < c)
                    return false;
            }

            return false;
        }
    }
}
