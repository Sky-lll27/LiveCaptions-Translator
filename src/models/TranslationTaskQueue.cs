using LiveCaptionsTranslator.utils;

namespace LiveCaptionsTranslator.models
{
    public class TranslationTaskQueue
    {
        private readonly object _lock = new object();
        private readonly List<TranslationTask> tasks;

        private (string translatedText, bool isChoke) output;
        public (string translatedText, bool isChoke) Output => output;

        public TranslationTaskQueue()
        {
            tasks = new List<TranslationTask>();
            output = (string.Empty, false);
        }

        public void Enqueue(Func<CancellationToken, Task<(string, bool)>> worker, string originalText)
        {
            var newTranslationTask = new TranslationTask(worker, originalText, new CancellationTokenSource());
            lock (_lock)
            {
                tasks.Add(newTranslationTask);
            }
            // Run `OnTaskCompleted` in a new thread.
            newTranslationTask.Task.ContinueWith(
                task => OnTaskCompleted(newTranslationTask),
                TaskContinuationOptions.OnlyOnRanToCompletion
            );
        }

        private async Task OnTaskCompleted(TranslationTask translationTask)
        {
            lock (_lock)
            {
                var index = tasks.IndexOf(translationTask);
                for (int i = 0; i < index; i++)
                    tasks[i].CTS.Cancel();
                for (int i = index; i >= 0; i--)
                    tasks.RemoveAt(i);
            }

            output = translationTask.Task.Result;
            var translatedText = output.Item1;

            // Log after translation.
            // 写历史前去掉界面上的延迟前缀（形如 "[  98 ms] "），
            // 否则历史记录和导出的 CSV 里会混进延迟数字；显示仍然保留前缀。
            // 用 LatencyPrefix() 而不是 NoticePrefix()：后者是贪婪匹配，
            // 译文正文里若带方括号会被一起吃掉。
            string loggedText = RegexPatterns.LatencyPrefix().Replace(translatedText, string.Empty);

            // 报错和「模型没翻、把原文抄回来」的结果不写入历史。
            // 上游会把这两种都当成功存进去：库里 1495 条里有 288 条是 [ERROR]，
            // 另有一批译文与原文完全相同，翻历史时很难分辨哪条是真译文。
            // 显示保持不变（用户仍然看得到失败提示），只是不污染历史库。
            bool isFailure = loggedText.Contains("[ERROR]") || loggedText.Contains("[WARNING]");
            bool isUntranslated = !isFailure && TranslationQuality.IsUntranslated(
                translationTask.OriginalText, loggedText, Translator.Setting?.TargetLanguage ?? "zh-CN");
            if (isFailure || isUntranslated)
                return;

            bool isOverwrite = await Translator.IsOverwrite(translationTask.OriginalText);
            if (!isOverwrite)
                await Translator.AddContexts();
            await Translator.Log(translationTask.OriginalText, loggedText, isOverwrite);
        }
    }

    public class TranslationTask
    {
        public Task<(string, bool)> Task { get; }
        public string OriginalText { get; }
        public CancellationTokenSource CTS { get; }

        public TranslationTask(Func<CancellationToken, Task<(string, bool)>> worker,
            string originalText, CancellationTokenSource cts)
        {
            Task = worker(cts.Token);
            OriginalText = originalText;
            CTS = cts;
        }
    }
}