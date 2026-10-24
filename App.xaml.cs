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

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Load configurations first
            _config = LoadConfiguration();

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

        private BannerConfig LoadConfiguration()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

            if (!File.Exists(configPath))
            {
                return new BannerConfig();
            }

            try
            {
                string jsonText = File.ReadAllText(configPath);
                var parsedConfig = System.Text.Json.JsonSerializer.Deserialize<BannerConfig>(jsonText);
                return parsedConfig ?? new BannerConfig();
            }
            catch
            {
                return new BannerConfig(); // Safe fallback to hardcoded defaults on parsing issues
            }
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
                        string expectedValue = $"\"{executablePath}\"";

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
