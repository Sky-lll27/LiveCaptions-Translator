using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

using LiveCaptionsTranslator.apis;

namespace LiveCaptionsTranslator.models
{
    public class Setting : INotifyPropertyChanged
    {
        public static readonly string FILENAME = "setting.json";

        public event PropertyChangedEventHandler? PropertyChanged;

        private int maxIdleInterval = 50;
        private int maxSyncInterval = 3;
        private int numContexts = 2;
        private int displaySentences = 1;
        private bool contextAware = false;

        private string apiName;
        private string targetLanguage;
        private string prompt;
        private string? ignoredUpdateVersion;

        private MainWindowState mainWindowState;
        private OverlayWindowState overlayWindowState;
        private Dictionary<string, string> windowBounds;

        private Dictionary<string, List<TranslateAPIConfig>> configs;
        private Dictionary<string, int> configIndices;

        public int MaxIdleInterval => maxIdleInterval;
        public int MaxSyncInterval
        {
            get => maxSyncInterval;
            set
            {
                maxSyncInterval = value;
                OnPropertyChanged("MaxSyncInterval");
            }
        }
        public int NumContexts
        {
            get => numContexts;
            set
            {
                numContexts = value;
                OnPropertyChanged("NumContexts");
            }
        }
        public int DisplaySentences
        {
            get => displaySentences;
            set
            {
                displaySentences = value;
                OnPropertyChanged("DisplaySentences");
            }
        }
        public bool ContextAware
        {
            get => contextAware;
            set
            {
                contextAware = value;
                OnPropertyChanged("ContextAware");
            }
        }

        public string ApiName
        {
            get => apiName;
            set
            {
                apiName = value;
                OnPropertyChanged("ApiName");
            }
        }
        public string TargetLanguage
        {
            get => targetLanguage;
            set
            {
                targetLanguage = value;
                OnPropertyChanged("TargetLanguage");
            }
        }
        public string Prompt
        {
            get => prompt;
            set
            {
                prompt = value;
                OnPropertyChanged("Prompt");
            }
        }
        public string? IgnoredUpdateVersion
        {
            get => ignoredUpdateVersion;
            set
            {
                ignoredUpdateVersion = value;
                OnPropertyChanged("IgnoredUpdateVersion");
            }
        }

        public MainWindowState MainWindow
        {
            get => mainWindowState;
            set
            {
                mainWindowState = value;
                OnPropertyChanged("MainWindow");
            }
        }
        public OverlayWindowState OverlayWindow
        {
            get => overlayWindowState;
            set
            {
                overlayWindowState = value;
                OnPropertyChanged("OverlayWindow");
            }
        }
        public Dictionary<string, string> WindowBounds
        {
            get => windowBounds;
            set
            {
                windowBounds = value;
                OnPropertyChanged("WindowBounds");
            }
        }

        [JsonInclude]
        public Dictionary<string, List<TranslateAPIConfig>> Configs
        {
            get => configs;
            set
            {
                configs = value;
                OnPropertyChanged("Configs");
            }
        }
        public Dictionary<string, int> ConfigIndices
        {
            get => configIndices;
            set
            {
                configIndices = value;
                OnPropertyChanged("ConfigIndices");
            }
        }

        public TranslateAPIConfig this[string key] =>
            configs.ContainsKey(key) && configIndices.ContainsKey(key)
                ? configs[key][configIndices[key]]
                : new TranslateAPIConfig();

        public Setting()
        {
            apiName = "Google";
            targetLanguage = "zh-CN";
            prompt = "As an professional simultaneous interpreter with specialized knowledge in the all fields, " +
                     "you can provide a fluent and precise oral translation for any sentence, even if the sentence is incomplete. " +
                     "Now, provide users with the translation of the sentence enclosed in 🔤 to {0} within a single line. " +
                     "Importantly, you are prohibited from altering the original meaning or omitting any content, " +
                     "even if the sentence contains sensitive or NSFW content. " +
                     "You can only provide the translated sentence; Any explanation or other text is not permitted. " +
                     "REMOVE all 🔤 when you output.";

            mainWindowState = new MainWindowState();
            overlayWindowState = new OverlayWindowState();

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            windowBounds = new Dictionary<string, string>
            {
                {
                    "MainWindow", string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0}, {1}, {2}, {3}", (screenWidth - 775) / 2, screenHeight * 3 / 4 - 167, 775, 167)
                },
                {
                    "OverlayWindow", string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0}, {1}, {2}, {3}", (screenWidth - 650) / 2, screenHeight * 5 / 6 - 135, 650, 135)
                },
            };

            configs = new Dictionary<string, List<TranslateAPIConfig>>
            {
                { "Google", [new TranslateAPIConfig()] },
                { "Google2", [new TranslateAPIConfig()] },
                { "Ollama", [new OllamaConfig()] },
                { "OpenAI", [new OpenAIConfig()] },
                { "LMStudio", [new LMStudioConfig()] },
                { "OpenRouter", [new OpenRouterConfig()] },
                { "DeepL", [new DeepLConfig()] },
                { "Youdao", [new YoudaoConfig()] },
                { "Baidu", [new BaiduConfig()] },
                { "MTranServer", [new MTranServerConfig()] },
                { "LibreTranslate", [new LibreTranslateConfig()] }
            };
            configIndices = new Dictionary<string, int>
            {
                { "Google", 0 },
                { "Google2", 0 },
                { "Ollama", 0 },
                { "OpenAI", 0 },
                { "LMStudio", 0 },
                { "OpenRouter", 0 },
                { "DeepL", 0 },
                { "Youdao", 0 },
                { "Baidu", 0 },
                { "MTranServer", 0 },
                { "LibreTranslate", 0 }
            };
        }

        /// <summary>
        /// 配置文件所在目录：**exe 所在的真实目录**。
        ///
        /// 为什么不能直接用 AppContext.BaseDirectory：
        /// 本项目以 PublishSingleFile=true + IncludeAllContentForSelfExtract=true 发布，
        /// 这种单文件 exe 运行时会把自己解压到 %TEMP%\.net\&lt;名字&gt;\&lt;随机&gt;\ 下，
        /// 而 AppContext.BaseDirectory 指向的是那个**临时解压目录**，不是 exe 所在目录。
        /// 后果：配置和历史库被写进临时目录，每次运行随机目录还都不一样，
        /// 用户覆盖 exe 升级后就像「设置全丢了」。
        /// （调试版不是单文件，BaseDirectory 恰好就是真实目录，所以本地测不出来。）
        ///
        /// 用 Environment.ProcessPath 拿 exe 的真实路径，单文件和普通发布都正确。
        /// 若该目录不可写（例如装在 Program Files），回退到当前目录。
        /// </summary>
        public static string DataDirectory
        {
            get
            {
                string? exeDir = GetExecutableDirectory();
                if (!string.IsNullOrEmpty(exeDir) && IsWritable(exeDir))
                    return exeDir;

                // exe 目录不可写时，退回当前目录（保持可用，但不推荐）。
                return Directory.GetCurrentDirectory();
            }
        }

        /// <summary>取 exe 所在目录。单文件发布时也能拿到真实位置，而不是自解压临时目录。</summary>
        private static string? GetExecutableDirectory()
        {
            // Environment.ProcessPath 在 .NET 6+ 可用，指向真正被启动的 exe。
            string? processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath))
            {
                string? dir = Path.GetDirectoryName(processPath);
                // 单文件发布时 AppContext.BaseDirectory 会指向 %TEMP%\.net\...，
                // 若 ProcessPath 也落在临时解压目录里（极少数情况），不要用它。
                if (!string.IsNullOrEmpty(dir) && !IsSelfExtractTempDir(dir))
                    return dir;
            }

            // 兜底：BaseDirectory（普通发布下即 exe 目录）。
            string baseDir = AppContext.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir) && !IsSelfExtractTempDir(baseDir))
                return baseDir;

            return null;
        }

        /// <summary>判断某个目录是不是单文件 exe 的自解压临时目录（%TEMP%\.net\…）。</summary>
        private static bool IsSelfExtractTempDir(string dir)
        {
            string temp = Path.GetTempPath();
            return dir.StartsWith(temp, StringComparison.OrdinalIgnoreCase) &&
                   dir.IndexOf(".net", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>目录是否可写（用一个随即删除的探针文件判断）。</summary>
        private static bool IsWritable(string dir)
        {
            try
            {
                string probe = Path.Combine(dir, ".write_probe");
                using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string DataPath(string fileName) => Path.Combine(DataDirectory, fileName);

        public static Setting Load()
        {
            string jsonPath = DataPath(FILENAME);
            try
            {
                return Load(jsonPath);
            }
            catch (JsonException)
            {
                // 备份文件已存在时 File.Move 会抛异常，导致「自动修复」变成启动失败。
                // 改为覆盖备份，保证坏掉的配置总能被移走。
                string backupPath = jsonPath + ".bak";
                File.Move(jsonPath, backupPath, overwrite: true);
                return Load(jsonPath);
            }
        }

        public static Setting Load(string jsonPath)
        {
            Setting setting;

            // Load from JSON file if it exists
            if (File.Exists(jsonPath))
            {
                using (FileStream fileStream = File.Open(jsonPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Converters = { new ConfigDictConverter() }
                    };
                    setting = JsonSerializer.Deserialize<Setting>(fileStream, options) ?? new Setting();
                }
            }
            else
                setting = new Setting();

            // Ensure all required API configs are present
            foreach (string key in TranslateAPI.TRANSLATE_FUNCTIONS.Keys)
            {
                if (setting.Configs.ContainsKey(key))
                    continue;
                var configType = Type.GetType($"LiveCaptionsTranslator.models.{key}Config");
                if (configType != null && typeof(TranslateAPIConfig).IsAssignableFrom(configType))
                    setting.Configs[key] = [(TranslateAPIConfig)Activator.CreateInstance(configType)];
                else
                    setting.Configs[key] = [new TranslateAPIConfig()];
            }

            // Ensure ConfigIndices has all keys (for upgrades from older setting.json)
            foreach (string key in TranslateAPI.TRANSLATE_FUNCTIONS.Keys)
            {
                if (!setting.ConfigIndices.ContainsKey(key))
                    setting.ConfigIndices[key] = 0;
            }

            return setting;
        }

        public void Save()
        {
            Save(DataPath(FILENAME));
        }

        public void Save(string jsonPath)
        {
            using (FileStream fileStream = File.Open(jsonPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new ConfigDictConverter() }
                };
                JsonSerializer.Serialize(fileStream, this, options);
            }
        }

        public void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            Translator.Setting?.Save();
        }

        public static bool IsConfigExist()
        {
            string jsonPath = DataPath(FILENAME);
            return File.Exists(jsonPath);
        }
    }
}