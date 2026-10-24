using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace ClassificationBanner
{
    public partial class BannerWindow : Window
    {
        private AppBarHelper.ABEdge _edge;
        private System.Windows.Forms.Screen _screen;
        private BannerConfig _config;
        private DispatcherTimer _zOrderTimer;
        private IntPtr _hwnd;

        public BannerWindow(System.Windows.Forms.Screen screen, AppBarHelper.ABEdge edge, BannerConfig config)
        {
            InitializeComponent();
            _screen = screen;
            _edge = edge;
            _config = config;

            // Apply configuration parameters dynamically
            ApplyConfigStyles();

            // Set up placement layout calculations
            this.Left = _screen.Bounds.Left;
            this.Width = _screen.Bounds.Width;
            this.Height = _config.BannerHeight;
            this.Top = (_edge == AppBarHelper.ABEdge.ABE_TOP) 
                ? _screen.Bounds.Top 
                : _screen.Bounds.Bottom - _config.BannerHeight;

            this.SourceInitialized += BannerWindow_SourceInitialized;
            this.Closing += BannerWindow_Closing;

            // Initialize Background Z-Order Enforcement Loop
            _zOrderTimer = new DispatcherTimer(DispatcherPriority.Normal);
            _zOrderTimer.Interval = TimeSpan.FromMilliseconds(_config.EnforcementIntervalMs);
            _zOrderTimer.Tick += EnforceAlwaysOnTopLoop;
            _zOrderTimer.Start();
        }

        private void ApplyConfigStyles()
        {
            try
            {
                var bc = new BrushConverter();
                this.Background = (System.Windows.Media.Brush)bc.ConvertFromString(_config.BackgroundColorHex)!;
                BannerText.Foreground = (System.Windows.Media.Brush)bc.ConvertFromString(_config.ForegroundColorHex)!;
            }
            catch
            {
                // Fallback UI defaults if configuration hex values are corrupt
                this.Background = System.Windows.Media.Brushes.Red;
                BannerText.Foreground = System.Windows.Media.Brushes.White;
            }

            BannerText.Text = _config.ClassificationText;
        }

        private void BannerWindow_SourceInitialized(object? sender, EventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;

            AppBarHelper.RECT rect = new AppBarHelper.RECT
            {
                Left = _screen.Bounds.Left,
                Right = _screen.Bounds.Right,
                Top = (_edge == AppBarHelper.ABEdge.ABE_TOP) ? _screen.Bounds.Top : _screen.Bounds.Bottom - _config.BannerHeight,
                Bottom = (_edge == AppBarHelper.ABEdge.ABE_TOP) ? _screen.Bounds.Top + _config.BannerHeight : _screen.Bounds.Bottom
            };

            AppBarHelper.RegisterBar(_hwnd, rect, _edge);
        }

        private void EnforceAlwaysOnTopLoop(object? sender, EventArgs e)
        {
            if (_hwnd == IntPtr.Zero) return;

            // Force window to the top of the Z-order array without stealing key focus from the active user application
            AppBarHelper.SetWindowPos(
                _hwnd, 
                AppBarHelper.HWND_TOPMOST, 
                0, 0, 0, 0, 
                AppBarHelper.SWP_NOMOVE | AppBarHelper.SWP_NOSIZE | AppBarHelper.SWP_NOACTIVATE
            );

            // Double check framework layout synchronization state
            if (!this.Topmost)
            {
                this.Topmost = false;
                this.Topmost = true;
            }
        }

        private void BannerWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _zOrderTimer.Stop();
            if (_hwnd != IntPtr.Zero)
            {
                AppBarHelper.UnregisterBar(_hwnd);
            }
        }
    }
}
