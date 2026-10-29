// zh-CN fork: 本文件由 Sky-lll27 修改（2026-10），更新检查改为指向本汉化仓库，
// 版本比较由「字符串不等」改为「按版本号逐段比较」。
// Based on SakiRinn/LiveCaptions-Translator (Apache-2.0). Modified per §4(b).

using System.Net.Http;
using System.Text.Json;

namespace LiveCaptionsTranslator.utils
{
    public static class UpdateUtil
    {
        // 本汉化版：更新检查指向自己的仓库，避免把用户引导去下载无汉化、无「文本」页的上游版本。
        public const string GitHubRepoUrl = "https://github.com/Sky-lll27/LiveCaptions-Translator";
        public const string GitHubReleasesUrl = "https://github.com/Sky-lll27/LiveCaptions-Translator/releases";
        public const string GitHubLatestReleaseApi = "https://api.github.com/repos/Sky-lll27/LiveCaptions-Translator/releases/latest";

        public static async Task<string> GetLatestVersion()
        {
            string apiUrl = GitHubLatestReleaseApi;

            using var client = new HttpClient()
            {
                Timeout = TimeSpan.FromSeconds(3)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("LiveCaptions-Translator");
            var response = await client.GetStringAsync(apiUrl);
            using var doc = JsonDocument.Parse(response);
            var latestVersionRaw = doc.RootElement.GetProperty("tag_name").GetString();
            var latestVersion = string.IsNullOrEmpty(latestVersionRaw)
                ? String.Empty
                : RegexPatterns.VersionNumber().Replace(latestVersionRaw, "");
            return latestVersion;
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
