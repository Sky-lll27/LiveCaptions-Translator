using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Appearance;

using LiveCaptionsTranslator.models;
using LiveCaptionsTranslator.utils;
using Wpf.Ui.Controls;

namespace LiveCaptionsTranslator
{
    public partial class SettingPage : Page
    {
        private static SettingWindow? SettingWindow;

        public SettingPage()
        {
            InitializeComponent();
            ApplicationThemeManager.ApplySystemTheme();
            DataContext = Translator.Setting;

            Loaded += (s, e) =>
            {
                (App.Current.MainWindow as MainWindow)?.AutoHeightAdjust(maxHeight: (int)App.Current.MainWindow.MinHeight);
                CheckForFirstUse();
            };

            TranslateAPIBox.ItemsSource = Translator.Setting?.Configs.Keys;
            TranslateAPIBox.SelectedIndex = 0;

            LoadAPISetting();
        }

        private void LiveCaptionsButton_click(object sender, RoutedEventArgs e)
        {
            if (Translator.Window == null)
                return;

            var button = sender as Wpf.Ui.Controls.Button;
            var text = ButtonText.Text;

            bool isHide = Translator.Window.Current.BoundingRectangle == Rect.Empty;
            if (isHide)
            {
                LiveCaptionsHandler.RestoreLiveCaptions(Translator.Window);
                ButtonText.Text = "隐藏";
            }
            else
            {
                LiveCaptionsHandler.HideLiveCaptions(Translator.Window);
                ButtonText.Text = "显示";
            }
        }

        private void TranslateAPIBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadAPISetting();
        }

        /// <summary>
        /// 目标语言下拉框的显示名。键是内部使用的语言代码（会存进 setting.json，
        /// 也会被 Google 等渠道直接拼进 URL），值是给用户看的中文名。
        /// 代码本身不能改：Google/Google2 拿它当 tl= 参数，TranslateAPI 也用它查语言名。
        /// </summary>
        private static readonly Dictionary<string, string> TargetLangDisplayNames = new()
        {
            { "zh-CN", "简体中文" },
            { "zh-TW", "繁体中文" },
            { "en-US", "英语（美国）" },
            { "en-GB", "英语（英国）" },
            { "ja-JP", "日语" },
            { "ko-KR", "韩语" },
            { "fr-FR", "法语" },
            { "th-TH", "泰语" },
            { "ru-RU", "俄语" },
            { "es-ES", "西班牙语" },
            { "pt-BR", "葡萄牙语" },
            { "tr-TR", "土耳其语" },
            { "ar-SA", "阿拉伯语" },
        };

        /// <summary>下拉框里的一项：对用户显示中文名，实际取值是语言代码。</summary>
        private sealed class TargetLangOption
        {
            public required string Code { get; init; }
            public required string Display { get; init; }
            public override string ToString() => Display;
        }

        private void TargetLangBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TargetLangBox.SelectedItem is TargetLangOption option)
                Translator.Setting.TargetLanguage = option.Code;
            else if (TargetLangBox.SelectedItem != null)
                Translator.Setting.TargetLanguage = TargetLangBox.SelectedItem.ToString();
        }

        private void TargetLangBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // 用户可能手输。手输的可能是中文显示名，也可能是语言代码（BCP 47），
            // 两种都要能正确落到 TargetLanguage 上。
            string text = (TargetLangBox.Text ?? string.Empty).Trim();
            if (text.Length == 0)
                return;

            foreach (var option in TargetLangOptions)
            {
                if (option.Display == text || option.Code == text)
                {
                    Translator.Setting.TargetLanguage = option.Code;
                    return;
                }
            }

            // 不在列表里：当作自定义语言代码原样保存（保持上游行为）。
            Translator.Setting.TargetLanguage = text;
        }

        private List<TargetLangOption> targetLangOptions = new();
        private List<TargetLangOption> TargetLangOptions => targetLangOptions;

        private void APISettingButton_click(object sender, RoutedEventArgs e)
        {
            if (SettingWindow != null && SettingWindow.IsLoaded)
                SettingWindow.Activate();
            else
            {
                SettingWindow = new SettingWindow();
                SettingWindow.Closed += (sender, args) => SettingWindow = null;
                SettingWindow.Show();
            }
        }

        private void Contexts_ValueChanged(object sender, NumberBoxValueChangedEventArgs args)
        {
            if (Translator.Setting.DisplaySentences > Translator.Setting.NumContexts)
                Translator.Setting.DisplaySentences = Translator.Setting.NumContexts;
        }

        private void DisplaySentences_ValueChanged(object sender, NumberBoxValueChangedEventArgs args)
        {
            if (Translator.Setting.DisplaySentences > Translator.Setting.NumContexts)
                Translator.Setting.NumContexts = Translator.Setting.DisplaySentences;
            Translator.Caption.OnPropertyChanged("DisplayLogCards");
            Translator.Caption.OnPropertyChanged("OverlayPreviousTranslation");
        }

        private void LiveCaptionsInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            LiveCaptionsInfoFlyout.Show();
        }

        private void LiveCaptionsInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            LiveCaptionsInfoFlyout.Hide();
        }

        private void FrequencyInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            FrequencyInfoFlyout.Show();
        }

        private void FrequencyInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            FrequencyInfoFlyout.Hide();
        }

        private void TranslateAPIInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            TranslateAPIInfoFlyout.Show();
        }

        private void TranslateAPIInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            TranslateAPIInfoFlyout.Hide();
        }

        private void TargetLangInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            TargetLangInfoFlyout.Show();
        }

        private void TargetLangInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            TargetLangInfoFlyout.Hide();
        }

        private void CaptionLogMaxInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            CaptionLogMaxInfoFlyout.Show();
        }

        private void CaptionLogMaxInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            CaptionLogMaxInfoFlyout.Hide();
        }

        private void ContextAwareInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            ContextAwareInfoFlyout.Show();
        }

        private void ContextAwareInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            ContextAwareInfoFlyout.Hide();
        }

        private void CheckForFirstUse()
        {
            if (Translator.FirstUseFlag)
                ButtonText.Text = "隐藏";
        }

        public void LoadAPISetting()
        {
            var configType = Translator.Setting[Translator.Setting.ApiName].GetType();
            var languagesProp = configType.GetProperty(
                "SupportedLanguages", BindingFlags.Public | BindingFlags.Static);

            // Traverse base classes to find `SupportedLanguages`
            while (configType != null && languagesProp == null)
            {
                configType = configType.BaseType;
                languagesProp = configType.GetProperty(
                    "SupportedLanguages", BindingFlags.Public | BindingFlags.Static);
            }
            if (languagesProp == null)
                languagesProp = typeof(TranslateAPIConfig).GetProperty(
                    "SupportedLanguages", BindingFlags.Public | BindingFlags.Static);

            var supportedLanguages = (Dictionary<string, string>)languagesProp.GetValue(null);

            // 下拉框显示中文名，取值仍是语言代码。
            // 不在映射表里的语言（自定义的）原样显示，保证不会因为汉化而丢掉选项。
            targetLangOptions = new List<TargetLangOption>();
            foreach (string code in supportedLanguages.Keys)
            {
                targetLangOptions.Add(new TargetLangOption
                {
                    Code = code,
                    Display = TargetLangDisplayNames.TryGetValue(code, out var name) ? name : code,
                });
            }

            string targetLang = Translator.Setting.TargetLanguage;
            if (!supportedLanguages.ContainsKey(targetLang))
                supportedLanguages[targetLang] = targetLang;    // add custom language to supported languages

            // 自定义语言也要进下拉框（显示原始代码），否则打开设置页时选不中。
            if (targetLangOptions.All(o => o.Code != targetLang))
                targetLangOptions.Add(new TargetLangOption { Code = targetLang, Display = targetLang });

            TargetLangBox.ItemsSource = targetLangOptions;
            TargetLangBox.SelectedItem = targetLangOptions.FirstOrDefault(o => o.Code == targetLang);
        }
    }
}