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
        private AppBarHelper.RECT _grantedRect;
        private bool _placed;

        public BannerWindow(System.Windows.Forms.Screen screen, AppBarHelper.ABEdge edge, BannerConfig config)
        {
            InitializeComponent();
            _screen = screen;
            _edge = edge;
            _config = config;

            // Apply configuration parameters dynamically
            ApplyConfigStyles();

            // Suppress WPF's own placement logic. Screen.Bounds is in physical pixels while
            // Window.Top/Left are in DIPs, so final placement is performed with SetWindowPos
            // once the real DPI transform is known.
            this.WindowStartupLocation = WindowStartupLocation.Manual;
            this.SizeToContent = SizeToContent.Manual;

            this.SourceInitialized += BannerWindow_SourceInitialized;
            this.Loaded += BannerWindow_Loaded;
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

            // BannerHeight is authored in device-independent units; convert to physical
            // pixels for this window's monitor so the bar is the same apparent size at
            // every scaling factor.
            double scaleY = GetScaleY();

            int thickness = (int)Math.Round(_config.BannerHeight * scaleY);
            if (thickness < 1) thickness = 1;

            // Request the full screen edge. The shell trims this during ABM_QUERYPOS so the
            // bar does not overlap the taskbar or any previously registered AppBar.
            AppBarHelper.RECT desired = new AppBarHelper.RECT
            {
                Left = _screen.Bounds.Left,
                Right = _screen.Bounds.Right,
                Top = (_edge == AppBarHelper.ABEdge.ABE_TOP)
                    ? _screen.Bounds.Top
                    : _screen.Bounds.Bottom - thickness,
                Bottom = (_edge == AppBarHelper.ABEdge.ABE_TOP)
                    ? _screen.Bounds.Top + thickness
                    : _screen.Bounds.Bottom
            };

            _grantedRect = AppBarHelper.RegisterBar(_hwnd, desired, _edge, thickness);

            // Keep WPF's managed layout properties in agreement with the native rectangle.
            // Without this, the next layout pass re-applies the stale default values and
            // moves the window off the reserved area.
            SyncManagedBounds();
        }

        private void BannerWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            // SourceInitialized runs before WPF has finished its own window placement, so
            // positioning there is overwritten during Show(). Re-assert it here, once the
            // framework has settled.
            _placed = true;
            ApplyPlacement();
        }

        private double GetScaleY()
        {
            if (PresentationSource.FromVisual(this) is HwndSource source && source.CompositionTarget != null)
            {
                double m22 = source.CompositionTarget.TransformToDevice.M22;
                if (m22 > 0) return m22;
            }

            return 1.0;
        }

        private void SyncManagedBounds()
        {
            double scaleY = GetScaleY();
            double scaleX = 1.0;

            if (PresentationSource.FromVisual(this) is HwndSource source && source.CompositionTarget != null)
            {
                double m11 = source.CompositionTarget.TransformToDevice.M11;
                if (m11 > 0) scaleX = m11;
            }

            this.Left = _grantedRect.Left / scaleX;
            this.Top = _grantedRect.Top / scaleY;
            this.Width = _grantedRect.Width / scaleX;
            this.Height = _grantedRect.Height / scaleY;
        }

        /// <summary>
        /// Moves the window onto the rectangle the shell granted and notifies the shell.
        /// </summary>
        private void ApplyPlacement()
        {
            if (_hwnd == IntPtr.Zero) return;

            AppBarHelper.SetWindowPos(
                _hwnd,
                AppBarHelper.HWND_TOPMOST,
                _grantedRect.Left,
                _grantedRect.Top,
                _grantedRect.Width,
                _grantedRect.Height,
                AppBarHelper.SWP_NOACTIVATE);

            AppBarHelper.NotifyPosChanged(_hwnd);
        }

        private void EnforceAlwaysOnTopLoop(object? sender, EventArgs e)
        {
            if (_hwnd == IntPtr.Zero || !_placed) return;

            // Re-assert position and size as well as Z-order. Previously this used
            // SWP_NOMOVE | SWP_NOSIZE, so once the window drifted off the reserved
            // area nothing ever corrected it.
            AppBarHelper.SetWindowPos(
                _hwnd,
                AppBarHelper.HWND_TOPMOST,
                _grantedRect.Left,
                _grantedRect.Top,
                _grantedRect.Width,
                _grantedRect.Height,
                AppBarHelper.SWP_NOACTIVATE);

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
