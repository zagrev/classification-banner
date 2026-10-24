using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ClassificationBanner
{
    /// <summary>
    /// Application composition root. Excluded from coverage because it requires a live WPF
    /// message loop, the shell, and the registry; its decision logic is factored into
    /// <see cref="CommandLineOptions"/> and <see cref="ConfigurationLoader"/>.
    /// </summary>
    [ExcludeFromCodeCoverage]
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
            _configPathOverride = CommandLineOptions.ParseConfigPath(e.Args);

            // Load configurations first - terminates the process on an invalid explicit path
            var result = new ConfigurationLoader().Load(_configPathOverride);

            if (result.IsFatal)
            {
                FailFast(result.ErrorMessage ?? "The settings file could not be loaded.");
            }

            _config = result.Config;

            // Only reached when configuration is valid, so the registry entry is safe to write
            EnsureStartupRegistration();

            // Perform initial window distribution layout
            RebuildBanners();

            // Bind global operating system display hardware configuration change events
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
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
                        string expectedValue = CommandLineOptions.BuildStartupCommand(executablePath, _configPathOverride);

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
