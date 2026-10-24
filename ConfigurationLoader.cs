using System;
using System.IO;

namespace ClassificationBanner
{
    /// <summary>
    /// Outcome of a configuration load attempt.
    /// </summary>
    public enum ConfigLoadStatus
    {
        /// <summary>The settings file was read and parsed.</summary>
        Loaded,

        /// <summary>No file was found at the implicit default location; defaults apply.</summary>
        DefaultsUsed,

        /// <summary>
        /// An explicitly requested settings file was missing or unusable. Startup must abort
        /// rather than display defaults, which could understate the classification level.
        /// </summary>
        Fatal
    }

    public sealed class ConfigLoadResult
    {
        public ConfigLoadResult(BannerConfig config, ConfigLoadStatus status, string path, string? errorMessage = null)
        {
            Config = config;
            Status = status;
            Path = path;
            ErrorMessage = errorMessage;
        }

        public BannerConfig Config { get; }
        public ConfigLoadStatus Status { get; }
        public string Path { get; }
        public string? ErrorMessage { get; }
        public bool IsFatal => Status == ConfigLoadStatus.Fatal;
    }

    /// <summary>
    /// Resolves and loads banner configuration. File access is routed through delegates so
    /// the decision logic can be exercised without touching disk.
    /// </summary>
    public class ConfigurationLoader
    {
        private readonly Func<string, bool> _fileExists;
        private readonly Func<string, string> _readAllText;

        public ConfigurationLoader()
            : this(File.Exists, File.ReadAllText)
        {
        }

        public ConfigurationLoader(Func<string, bool> fileExists, Func<string, string> readAllText)
        {
            _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
            _readAllText = readAllText ?? throw new ArgumentNullException(nameof(readAllText));
        }

        /// <summary>
        /// The settings file used when no override is supplied.
        /// </summary>
        public static string DefaultConfigPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        /// <summary>
        /// Loads configuration. When <paramref name="overridePath"/> is supplied it takes
        /// precedence over the default settings file, and any failure to read it is fatal.
        /// Failures on the implicit default fall back to built-in defaults.
        /// </summary>
        public ConfigLoadResult Load(string? overridePath, string? defaultPath = null)
        {
            bool isExplicit = !string.IsNullOrEmpty(overridePath);
            string configPath = overridePath ?? defaultPath ?? DefaultConfigPath;

            if (!_fileExists(configPath))
            {
                return isExplicit
                    ? Fatal(configPath, $"The specified settings file could not be found:\n\n{configPath}")
                    : Defaults(configPath);
            }

            try
            {
                string jsonText = _readAllText(configPath);
                var parsed = System.Text.Json.JsonSerializer.Deserialize<BannerConfig>(jsonText);

                if (parsed == null)
                {
                    return isExplicit
                        ? Fatal(configPath, $"The settings file contains no configuration object:\n\n{configPath}")
                        : Defaults(configPath);
                }

                return new ConfigLoadResult(parsed, ConfigLoadStatus.Loaded, configPath);
            }
            catch (Exception ex)
            {
                return isExplicit
                    ? Fatal(configPath, $"The settings file could not be read:\n\n{configPath}\n\n{ex.Message}")
                    : Defaults(configPath);
            }
        }

        private static ConfigLoadResult Defaults(string path) =>
            new ConfigLoadResult(new BannerConfig(), ConfigLoadStatus.DefaultsUsed, path);

        private static ConfigLoadResult Fatal(string path, string message) =>
            new ConfigLoadResult(new BannerConfig(), ConfigLoadStatus.Fatal, path, message);
    }
}
