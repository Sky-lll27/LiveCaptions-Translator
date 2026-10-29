using System.Text.RegularExpressions;

namespace LiveCaptionsTranslator.utils
{
    public static partial class RegexPatterns
    {
        // Remove the `.` between two uppercase letters. (Cope with acronym)
        [GeneratedRegex(@"([A-Z])\s*\.\s*([A-Z])(?![A-Za-z]+)")]
        public static partial Regex Acronym();

        // If an acronym is followed by a word, preserve the space between them.
        [GeneratedRegex(@"([A-Z])\s*\.\s*([A-Z])(?=[A-Za-z]+)")]
        public static partial Regex AcronymWithWords();

        // Remove redundant spaces and `\n` around punctuation.
        [GeneratedRegex(@"\s*([.!?,])\s*")]
        public static partial Regex PunctuationSpace();

        // If it is Chinese or Japanese punctuation, no need to insert spaces.
        [GeneratedRegex(@"\s*([。！？，、])\s*")]
        public static partial Regex CJPunctuationSpace();

        [GeneratedRegex(@"^(\[.+\] )?(.*)$")]
        public static partial Regex NoticePrefixAndTranslation();

        [GeneratedRegex(@"^\[.+\] ")]
        public static partial Regex NoticePrefix();

        // 只匹配界面上的延迟前缀（形如 "[  98 ms] "）。
        // 写历史时用它剥离，不能复用 NoticePrefix()：后者是贪婪的 `^\[.+\] `，
        // 若译文正文里带方括号会被一并吃掉。
        [GeneratedRegex(@"^\[\s*\d+\s*ms\]\s")]
        public static partial Regex LatencyPrefix();

        [GeneratedRegex(@"^(https?:\/\/)")]
        public static partial Regex HttpPrefix();

        [GeneratedRegex(@"\/{2,}")]
        public static partial Regex MultipleSlashes();

        [GeneratedRegex(@"<think>.*?<\/think>")]
        public static partial Regex ModelThinking();

        [GeneratedRegex(@"[^0-9.]")]
        public static partial Regex VersionNumber();

        [GeneratedRegex(@"🔤\s*(.+?)\s*🔤")]
        public static partial Regex TargetSentence();
    }
}
