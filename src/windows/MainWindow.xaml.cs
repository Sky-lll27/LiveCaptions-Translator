using System.Diagnostics;
using System.Reflection;
using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

using LiveCaptionsTranslator.utils;
using LiveCaptionsTranslator.Utils;
using Button = Wpf.Ui.Controls.Button;

namespace LiveCaptionsTranslator
{
    public partial class MainWindow : FluentWindow
    {
        public OverlayWindow? OverlayWindow { get; set; } = null;
        public bool IsAutoHeight { get; set; } = true;

        public MainWindow()
        {
            InitializeComponent();
            ApplicationThemeManager.ApplySystemTheme();

            Loaded += (s, e) =>
            {
                SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, true);
                RootNavigation.Navigate(typeof(CaptionPage));
                IsAutoHeight = true;
                CheckForFirstUse();
                CheckForUpdates();
            };

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            var windowState = WindowHandler.LoadState(this, Translator.Setting);
            if (windowState.Left <= 0 || windowState.Left >= screenWidth ||
                windowState.Top <= 0 || windowState.Top >= screenHeight)
            {
                WindowHandler.RestoreState(this, new Rect(
                    (screenWidth - 775) / 2, screenHeight * 3 / 4 - 167, 775, 167));
            }
            else
                WindowHandler.RestoreState(this, windowState);

            ToggleTopmost(Translator.Setting.MainWindow.Topmost);
            ShowLogCard(Translator.Setting.MainWindow.CaptionLogEnabled);
        }

        private void TopmostButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleTopmost(!this.Topmost);
        }

        private void OverlayModeButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var symbolIcon = button?.Icon as SymbolIcon;

            if (OverlayWindow == null)
            {
                symbolIcon.Symbol = SymbolRegular.ClosedCaption24;
                symbolIcon.Filled = true;

                OverlayWindow = new OverlayWindow();
                OverlayWindow.SizeChanged +=
                    (s, e) => WindowHandler.SaveState(OverlayWindow, Translator.Setting);
                OverlayWindow.LocationChanged +=
                    (s, e) => WindowHandler.SaveState(OverlayWindow, Translator.Setting);

                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double screenHeight = SystemParameters.PrimaryScreenHeight;

                var windowState = WindowHandler.LoadState(OverlayWindow, Translator.Setting);
                if (windowState.Left <= 0 || windowState.Left >= screenWidth ||
                    windowState.Top <= 0 || windowState.Top >= screenHeight)
                {
                    WindowHandler.RestoreState(OverlayWindow, new Rect(
                        (screenWidth - 650) / 2, screenHeight * 5 / 6 - 135, 650, 135));
                }
                else
                    WindowHandler.RestoreState(OverlayWindow, windowState);

                OverlayWindow.Show();
            }
            else
            {
                symbolIcon.Symbol = SymbolRegular.ClosedCaptionOff24;
                symbolIcon.Filled = false;

                switch (OverlayWindow.OnlyMode)
                {
                    case CaptionVisible.TranslationOnly:
                        OverlayWindow.OnlyMode = CaptionVisible.SubtitleOnly;
                        OverlayWindow.OnlyMode = CaptionVisible.Both;
                        break;
                    case CaptionVisible.SubtitleOnly:
                        OverlayWindow.OnlyMode = CaptionVisible.Both;
                        break;
                }

                OverlayWindow.Close();
                OverlayWindow = null;
            }
        }

        private void LogOnlyButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var symbolIcon = button?.Icon as SymbolIcon;

            if (Translator.LogOnlyFlag)
            {
                Translator.LogOnlyFlag = false;
                symbolIcon.Filled = false;
            }
            else
            {
                Translator.LogOnlyFlag = true;
                symbolIcon.Filled = true;
            }

            Translator.ClearContexts();
        }

        private void CaptionLogButton_Click(object sender, RoutedEventArgs e)
        {
            Translator.Setting.MainWindow.CaptionLogEnabled = !Translator.Setting.MainWindow.CaptionLogEnabled;
            ShowLogCard(Translator.Setting.MainWindow.CaptionLogEnabled);
            CaptionPage.Instance?.AutoHeight();
        }

        private void MainWindow_LocationChanged(object sender, EventArgs e)
        {
            var window = sender as Window;
            WindowHandler.SaveState(window, Translator.Setting);
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            MainWindow_LocationChanged(sender, e);
            IsAutoHeight = false;
        }

        public void ToggleTopmost(bool enabled)
        {
            var button = TopmostButton as Button;
            var symbolIcon = button?.Icon as SymbolIcon;
            symbolIcon.Filled = enabled;
            this.Topmost = enabled;
            Translator.Setting.MainWindow.Topmost = enabled;
        }

        private void CheckForFirstUse()
        {
            if (!Translator.FirstUseFlag)
                return;

            RootNavigation.Navigate(typeof(SettingPage));
            LiveCaptionsHandler.RestoreLiveCaptions(Translator.Window);

            Dispatcher.InvokeAsync(() =>
            {
                var welcomeWindow = new WelcomeWindow
                {
                    Owner = this
                };
                welcomeWindow.Show();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private async Task CheckForUpdates()
        {
            if (Translator.FirstUseFlag)
                return;

            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
            var check = await UpdateUtil.CheckLatestAsync();

            // 更新检查要「说人话」：成功就说是不是最新版，失败就说清是哪一类失败，
            // 让用户知道自己该做什么（开代理 / 等一小时 / 去发行页手动看）。
            switch (check.Status)
            {
                case UpdateUtil.CheckStatus.Success:
                    if (UpdateUtil.IsNewerVersion(check.LatestVersion, currentVersion))
                    {
                        await ShowUpdateDialog(check.LatestVersion, currentVersion);
                    }
                    else
                    {
                        SnackbarHost.Show(
                            "已是最新版本",
                            $"当前版本 {currentVersion}，未发现更新的版本。",
                            SnackbarType.Success, timeout: 3);
                    }
                    return;

                case UpdateUtil.CheckStatus.RateLimited:
                    // 最常见的一种。GitHub 对未登录请求限额每小时 60 次，
                    // 短期内反复启动或刷新就会撞上。
                    SnackbarHost.Show(
                        "暂时无法检查更新",
                        "GitHub 接口访问次数已达上限（未登录时每小时 60 次），" +
                        "不影响翻译功能。请稍后再试，或直接到发行页手动查看最新版本。",
                        SnackbarType.Warning, width: 720, timeout: 6, closeButton: true);
                    return;

                case UpdateUtil.CheckStatus.NetworkError:
                    SnackbarHost.Show(
                        "暂时无法检查更新",
                        "连不上 GitHub，通常是代理未开启或网络不通。" +
                        "不影响翻译功能。可开启代理后重启，或直接到发行页手动查看最新版本。" +
                        $"\n（{check.Detail}）",
                        SnackbarType.Warning, width: 720, timeout: 6, closeButton: true);
                    return;

                case UpdateUtil.CheckStatus.NoRelease:
                    SnackbarHost.Show(
                        "暂时无法检查更新",
                        "远端仓库还没有可用的发行版本，或版本信息读取失败。" +
                        "不影响翻译功能，可到发行页确认。",
                        SnackbarType.Warning, width: 720, timeout: 6, closeButton: true);
                    return;

                default:
                    SnackbarHost.Show(
                        "暂时无法检查更新",
                        "更新检查遇到未预期的问题，不影响翻译功能。" +
                        $"可到发行页手动查看最新版本。\n（{check.Detail}）",
                        SnackbarType.Warning, width: 720, timeout: 6, closeButton: true);
                    return;
            }
        }

        private async Task ShowUpdateDialog(string latestVersion, string? currentVersion)
        {
            var ignoredVersion = Translator.Setting.IgnoredUpdateVersion;
            if (!string.IsNullOrEmpty(ignoredVersion) && ignoredVersion == latestVersion)
                return;

            var dialog = new Wpf.Ui.Controls.MessageBox
            {
                Title = "发现新版本",
                Content = $"检测到新版本：{latestVersion}\n" +
                          $"当前版本：{currentVersion}\n" +
                          $"请访问 GitHub 下载最新版本。",
                PrimaryButtonText = "去更新",
                CloseButtonText = "忽略此版本"
            };
            var result = await dialog.ShowDialogAsync();

            if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
            {
                var url = UpdateUtil.GitHubReleasesUrl;
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    SnackbarHost.Show("打开浏览器失败。", ex.Message, SnackbarType.Error,
                        timeout: 2, closeButton: true);
                }
            }
            else
                Translator.Setting.IgnoredUpdateVersion = latestVersion;
        }

        public void ShowLogCard(bool enabled)
        {
            if (CaptionLogButton.Icon is SymbolIcon icon)
            {
                if (enabled)
                    icon.Symbol = SymbolRegular.History24;
                else
                    icon.Symbol = SymbolRegular.HistoryDismiss24;
                CaptionPage.Instance?.CollapseTranslatedCaption(enabled);
            }
        }

        public void AutoHeightAdjust(int minHeight = -1, int maxHeight = -1)
        {
            if (minHeight > 0 && Height < minHeight)
            {
                Height = minHeight;
                IsAutoHeight = true;
            }

            if (IsAutoHeight && maxHeight > 0 && Height > maxHeight)
                Height = maxHeight;
        }
    }
}