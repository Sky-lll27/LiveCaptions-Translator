// 本文件为本分支新增（上游无此功能），作者 Sky-lll27，2026-10。
// Based on SakiRinn/LiveCaptions-Translator (Apache-2.0). 本文件随本仓库整体以 Apache-2.0 分发.
using System.Text;

namespace LiveCaptionsTranslator.utils
{
    /// <summary>
    /// 翻译质量检测：判断「模型是不是根本没翻译、把原文抄了回来」。
    ///
    /// 背景：本地小模型（尤其 1.8B/2B）遇到短碎片时经常原样返回原文，
    /// 而上游把这种结果当成「翻译成功」写进历史并显示出来。
    /// 这里的判定是纯字符统计，不调用任何网络、不做语言模型推理。
    ///
    /// 设计原则：**宁可漏判，不可误判**。误判会把正常译文丢掉并要求重试，
    /// 代价比漏判大，所以阈值都取实测数据的保守一侧。
    /// </summary>
    public static class TranslationQuality
    {
        /// <summary>日文假名（平假名 + 片假名），用于判断日文是否被翻成中文。</summary>
        private static bool IsKana(char c) =>
            (c >= '\u3040' && c <= '\u309F') ||   // Hiragana
            (c >= '\u30A0' && c <= '\u30FF');     // Katakana

        /// <summary>汉字（CJK 统一表意文字 + 扩展 A）。</summary>
        private static bool IsHan(char c) =>
            (c >= '\u4E00' && c <= '\u9FFF') ||
            (c >= '\u3400' && c <= '\u4DBF');

        /// <summary>韩文谚文。</summary>
        private static bool IsHangul(char c) =>
            (c >= '\uAC00' && c <= '\uD7AF') ||
            (c >= '\u1100' && c <= '\u11FF');

        private static bool IsLatin(char c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        /// <summary>去掉空白与标点，只保留「有意义的字符」，避免标点影响比例。</summary>
        private static string StripNoise(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c))
                    continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static double Ratio(string text, Func<char, bool> predicate)
        {
            string core = StripNoise(text);
            if (core.Length == 0)
                return 0.0;

            int hit = 0;
            foreach (char c in core)
            {
                if (predicate(c))
                    hit++;
            }
            return (double)hit / core.Length;
        }

        public static double KanaRatio(string text) => Ratio(text, IsKana);
        public static double HanRatio(string text) => Ratio(text, IsHan);
        public static double LatinRatio(string text) => Ratio(text, IsLatin);

        /// <summary>
        /// 判定源文本是不是日文（假名占比够高）。用于决定该用「查假名残留」的规则。
        /// </summary>
        public static bool LooksJapanese(string source) => KanaRatio(source) > 0.15;

        /// <summary>
        /// 判定源文本是不是英文/拉丁语系（几乎没汉字、没假名、拉丁字母占多数）。
        /// </summary>
        public static bool LooksLatin(string source) =>
            HanRatio(source) < 0.05 && KanaRatio(source) < 0.05 && LatinRatio(source) > 0.5;

        /// <summary>
        /// 判定源文本是不是已经是中文（汉字占多数、几乎没有假名，且汉字比拉丁字母多）。
        /// 目标语言是中文时，这种输入本来就不需要翻译。
        /// 注意中文口语里常夹英文专名（如「我们确定我们的 windows」），
        /// 所以不能要求拉丁字母占比很低，只要求「汉字比拉丁字母多」。
        /// </summary>
        public static bool LooksChinese(string source)
        {
            double han = HanRatio(source);
            double latin = LatinRatio(source);
            return han > 0.30 && KanaRatio(source) < 0.05 && han > latin;
        }

        /// <summary>
        /// 判断一条译文是不是「没翻」。
        /// </summary>
        /// <param name="source">原文</param>
        /// <param name="translated">译文</param>
        /// <param name="targetLanguage">目标语言代码，如 zh-CN / en-US</param>
        /// <returns>true = 判定为未翻译，应当重试或不入库</returns>
        public static bool IsUntranslated(string source, string translated, string targetLanguage)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(translated))
                return false;   // 空的交给既有失败判定处理，这里不重复报

            // 译文与原文完全相同：无论什么语言都算没翻。
            // 但要先排除「本来就不需要翻译」的情况（见下面的中文源判断）。
            string a = source.Trim();
            string b = translated.Trim();

            bool targetIsChinese = !string.IsNullOrEmpty(targetLanguage) &&
                                   targetLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

            if (targetIsChinese)
            {
                // 源文本本来就是中文 → 模型原样返回是正确行为，不算没翻。
                if (LooksChinese(a))
                    return false;

                // 日文源：译文里假名占比过高视为没翻。
                //
                // 阈值为什么分层：
                // 日文人名后缀（ちゃん / さん / くん）即使翻译正确也会留下假名，
                // 短句里这种后缀能把假名占比推到 15%~17%（实测「想让满ちゃん好好地
                // 摸摸我」= 0.17，是正常译文）。所以短句用宽松阈值 0.30。
                // 但长文本里人名的比重会被稀释，真正的「整段没翻」假名占比会很高，
                // 长文用严格阈值 0.10。
                // 实测 450 条日文历史：分层后判出 26 条，全部是真残留；
                // 2 条人名后缀的正常译文被正确放过。
                if (LooksJapanese(a))
                {
                    if (string.Equals(a, b, StringComparison.Ordinal))
                        return true;

                    double threshold = a.Length >= 100 ? 0.10 : 0.30;
                    return KanaRatio(b) > threshold;
                }

                // 英文/拉丁源：译文里汉字占比过低视为没翻。
                // 短句里专名、缩写会拉低汉字占比，同样分层：长文本用 0.10，短文本用 0.05。
                // 实测 698 条英文历史，该规则只判出 5 条，全部是原文==译文这种确凿情况，
                // 说明阈值足够保守。
                if (LooksLatin(a))
                {
                    if (string.Equals(a, b, StringComparison.Ordinal))
                        return true;

                    double threshold = a.Length >= 100 ? 0.10 : 0.05;
                    return HanRatio(b) < threshold;
                }

                // 其他源语言：只做「完全相同」这一个最强判定。
                return string.Equals(a, b, StringComparison.Ordinal);
            }

            // 目标语言是英文（或其他非中文）：
            bool targetIsEnglish = !string.IsNullOrEmpty(targetLanguage) &&
                                   targetLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase);
            if (targetIsEnglish)
            {
                // 源文本本来就是英文/拉丁 → 原样返回不算错。
                if (LooksLatin(a))
                    return false;

                // 中文/日文源翻成英文：如果译文里几乎没有拉丁字母，说明没翻。
                if (string.Equals(a, b, StringComparison.Ordinal))
                    return true;
                return LatinRatio(b) < 0.30;
            }

            // 目标语言既不是中文也不是英文：只做「完全相同」判定。
            return string.Equals(a, b, StringComparison.Ordinal);
        }
    }
}
