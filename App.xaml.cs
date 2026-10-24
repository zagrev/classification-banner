using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ClassificationBanner
{
    public partial class App : System.Windows.Application
    {
        private BannerConfig _config = new BannerConfig();
        private List<BannerWindow> _activeBanners = new List<BannerWindow>();
        private DispatcherTimer? _rebuildDebounceTimer;
        private string? _configPathOverride;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // There is no main window; RebuildBanners closes every window before
            // recreating them, which would otherwise terminate the process.
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Resolve any user-supplied settings file before configuration load
            _configPathOverride = ParseConfigPathArgument(e.Args);

            // Load configurations first - terminates the process on an invalid explicit path
            _config = LoadConfiguration(_configPathOverride);

            // Only reached when configuration is valid, so the registry entry is safe to write
            EnsureStartupRegistration();

            // Perform initial window distribution layout
            RebuildBanners();

            // Bind global operating system display hardware configuration change events
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        /// <summary>
        /// Extracts the settings file path from the command line. Accepts
        /// "--config &lt;path&gt;", "--config=&lt;path&gt;", the "-c" short form,
        /// or a single bare positional path. Returns null when unspecified.
        /// </summary>
        private static string? ParseConfigPathArgument(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (arg.StartsWith("--config=", StringComparison.OrdinalIgnoreCase))
                {
                    return NormalizePath(arg.Substring("--config=".Length));
                }

                if (arg.StartsWith("-c=", StringComparison.OrdinalIgnoreCase))
                {
                    return NormalizePath(arg.Substring("-c=".Length));
                }

                bool isConfigSwitch =
                    string.Equals(arg, "--config", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "-c", StringComparison.OrdinalIgnoreCase);

                if (isConfigSwitch && i + 1 < args.Length)
                {
                    return NormalizePath(args[i + 1]);
                }

                // Bare positional path, e.g. launched via file association or shortcut
                if (!arg.StartsWith("-", StringComparison.Ordinal))
                {
                    return NormalizePath(arg);
                }
            }

            return null;
        }

        private static string? NormalizePath(string candidate)
        {
            candidate = candidate.Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(candidate))
            {
                return null;
            }

            try
            {
                // Resolve relative paths against the working directory, not the exe directory
                return Path.GetFullPath(candidate);
            }
            catch
            {
                // Malformed path (invalid characters, too long, etc.)
                return null;
            }
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            // Debounce the layout reconfiguration. Display settings can trigger 
            // multiple event notifications sequentially within a few milliseconds.
            if (_rebuildDebounceTimer == null)
            {
                _rebuildDebounceTimer = new DispatcherTimer(DispatcherPriority.Normal);
                _rebuildDebounceTimer.Interval = TimeSpan.FromMilliseconds(500); // Wait for changes to settle
                _rebuildDebounceTimer.Tick += (s, args) =>
                {
                    _rebuildDebounceTimer.Stop();
                    RebuildBanners();
                };
            }

            _rebuildDebounceTimer.Stop();
            _rebuildDebounceTimer.Start();
        }

        private void RebuildBanners()
        {
            // 1. Tear down old windows to release SHAppBarMessage allocations cleanly
            foreach (var banner in _activeBanners)
            {
                try
                {
                    banner.Close();
                }
                catch
                {
                    // Fail-safe protection context
                }
            }
            _activeBanners.Clear();

            // 2. Query updated hardware layout topology maps 
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                // Top Screen Banner Allocation
                var topBanner = new BannerWindow(screen, AppBarHelper.ABEdge.ABE_TOP, _config);
                _activeBanners.Add(topBanner);
                topBanner.Show();

                // Bottom Screen Banner Allocation
                var bottomBanner = new BannerWindow(screen, AppBarHelper.ABEdge.ABE_BOTTOM, _config);
                _activeBanners.Add(bottomBanner);
                bottomBanner.Show();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Unbind operating system event hooks to prevent memory footprint leaks
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            base.OnExit(e);
        }

        /// <summary>
        /// Loads banner configuration. When <paramref name="overridePath"/> is supplied
        /// it takes precedence over the default appsettings.json next to the executable.
        /// An explicitly requested file that is missing or malformed is treated as fatal,
        /// since silently falling back to defaults could understate the classification level.
        /// </summary>
        private BannerConfig LoadConfiguration(string? overridePath)
        {
            bool isExplicit = !string.IsNullOrEmpty(overridePath);

            string configPath = overridePath
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

            if (!File.Exists(configPath))
            {
                if (isExplicit)
                {
                    FailFast($"The specified settings file could not be found:\n\n{configPath}");
                }

                return new BannerConfig();
            }

            try
            {
                string jsonText = File.ReadAllText(configPath);
                var parsedConfig = System.Text.Json.JsonSerializer.Deserialize<BannerConfig>(jsonText);

                if (parsedConfig == null)
                {
                    if (isExplicit)
                    {
                        FailFast($"The settings file contains no configuration object:\n\n{configPath}");
                    }

                    return new BannerConfig();
                }

                return parsedConfig;
            }
            catch (Exception ex)
            {
                if (isExplicit)
                {
                    FailFast($"The settings file could not be read:\n\n{configPath}\n\n{ex.Message}");
                }

                return new BannerConfig(); // Safe fallback to hardcoded defaults on parsing issues
            }
        }

        /// <summary>
        /// Reports an unrecoverable configuration error and terminates the process.
        /// Does not return.
        /// </summary>
        private void FailFast(string message)
        {
            MessageBox.Show(
                $"{message}\n\nThe classification banner will not start.",
                "Classification Banner - Configuration Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Environment.Exit(1);
        }

        private const string RegistryKeyName = "WindowsClassificationBanner";

        private void EnsureStartupRegistration()
        {
            try
            {
                // Access the current user's local startup registry branch
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    // Fetch the path of the currently executing compiled .exe binary
                    string? executablePath = Environment.ProcessPath;

                    if (!string.IsNullOrEmpty(executablePath))
                    {
                        // Check if it's already registered correctly
                        object? existingValue = key.GetValue(RegistryKeyName);

                        // Persist the settings file selection so it survives a reboot
                        string expectedValue = string.IsNullOrEmpty(_configPathOverride)
                            ? $"\"{executablePath}\""
                            : $"\"{executablePath}\" --config \"{_configPathOverride}\"";

                        if (existingValue == null || existingValue.ToString() != expectedValue)
                        {
                            // Inject or update the registry string value
                            key.SetValue(RegistryKeyName, expectedValue);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Fail silently or log if restricted by local user group policies
            }
        }
    }
}
