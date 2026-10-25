using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace ClassificationBanner
{
    /// <summary>
    /// Banner window host. Excluded from coverage because it requires a live WPF window and
    /// a registered shell AppBar; its placement arithmetic is factored into
    /// <see cref="BannerGeometry"/>.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public partial class BannerWindow : Window
    {
        private AppBarHelper.ABEdge _edge;
        private System.Windows.Forms.Screen _screen;
        private BannerConfig _config;
        private DispatcherTimer _zOrderTimer;
        private DispatcherTimer? _textRefreshTimer;
        private readonly ISystemInfoProvider _systemInfo;
        private IntPtr _hwnd;
        private AppBarHelper.RECT _grantedRect;
        private bool _placed;

        public BannerWindow(System.Windows.Forms.Screen screen, AppBarHelper.ABEdge edge, BannerConfig config)
            : this(screen, edge, config, new SystemInfoProvider())
        {
        }

        public BannerWindow(
            System.Windows.Forms.Screen screen,
            AppBarHelper.ABEdge edge,
            BannerConfig config,
            ISystemInfoProvider systemInfo)
        {
            InitializeComponent();
            _screen = screen;
            _edge = edge;
            _config = config;
            _systemInfo = systemInfo ?? new SystemInfoProvider();

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

            StartTextRefreshIfNeeded();
        }

        /// <summary>
        /// Starts a low-frequency refresh only when this edge's text contains a token whose
        /// value changes over time. Static text is never re-evaluated.
        /// </summary>
        private void StartTextRefreshIfNeeded()
        {
            if (_config.TextRefreshIntervalSeconds <= 0) return;

            BannerEdgeConfig edgeText = _config.GetEdge(_edge);

            if (!BannerTextResolver.ContainsVolatileToken(edgeText.Left)
                && !BannerTextResolver.ContainsVolatileToken(edgeText.Center)
                && !BannerTextResolver.ContainsVolatileToken(edgeText.Right))
            {
                return;
            }

            _textRefreshTimer = new DispatcherTimer(DispatcherPriority.Background);
            _textRefreshTimer.Interval = TimeSpan.FromSeconds(_config.TextRefreshIntervalSeconds);
            _textRefreshTimer.Tick += (s, e) => ApplyEdgeText();
            _textRefreshTimer.Start();
        }

        private void ApplyConfigStyles()
        {
            System.Windows.Media.Brush foreground;

            try
            {
                var bc = new BrushConverter();
                this.Background = (System.Windows.Media.Brush)bc.ConvertFromString(_config.BackgroundColorHex)!;
                foreground = (System.Windows.Media.Brush)bc.ConvertFromString(_config.ForegroundColorHex)!;
            }
            catch
            {
                // Fallback UI defaults if configuration hex values are corrupt
                this.Background = System.Windows.Media.Brushes.Red;
                foreground = System.Windows.Media.Brushes.White;
            }

            BannerTextLeft.Foreground = foreground;
            BannerText.Foreground = foreground;
            BannerTextRight.Foreground = foreground;

            ApplyEdgeText();
        }

        /// <summary>
        /// Resolves this edge's configured text, expanding tokens such as {hostname}.
        /// </summary>
        private void ApplyEdgeText()
        {
            BannerEdgeConfig edgeText = _config.GetEdge(_edge);

            BannerTextLeft.Text = BannerTextResolver.Resolve(edgeText.Left, _systemInfo);
            BannerText.Text = BannerTextResolver.Resolve(edgeText.Center, _systemInfo);
            BannerTextRight.Text = BannerTextResolver.Resolve(edgeText.Right, _systemInfo);
        }

        private void BannerWindow_SourceInitialized(object? sender, EventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;

            // BannerHeight is authored in device-independent units; convert to physical
            // pixels for this window's monitor so the bar is the same apparent size at
            // every scaling factor.
            double scaleY = GetScaleY();

            // get the height of the banner in physical pixels for the configured font size and scaling factor.
            // This is the thickness of the AppBar that will be requested from the shell.
            _config.BannerHeight = GetFontHeight(_config.Font);
            int thickness = BannerGeometry.ComputeThickness(_config.BannerHeight, scaleY);

            // Request the full screen edge. The shell trims this during ABM_QUERYPOS so the
            // bar does not overlap the taskbar or any previously registered AppBar.
            AppBarHelper.RECT desired = BannerGeometry.BuildDesiredRect(
                _screen.Bounds.Left,
                _screen.Bounds.Top,
                _screen.Bounds.Right,
                _screen.Bounds.Bottom,
                _edge,
                thickness);

            _grantedRect = AppBarHelper.RegisterBar(_hwnd, desired, _edge, thickness);

            // Keep WPF's managed layout properties in agreement with the native rectangle.
            // Without this, the next layout pass re-applies the stale default values and
            // moves the window off the reserved area.
            SyncManagedBounds();
        }

        private int GetFontHeight(FontSettings font)
        {
            FormattedText formattedText = new FormattedText(
                "Hg", // Use characters with ascenders and descenders to get full line bounds
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(font.Family),
                font.Size,
                Brushes.Black,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            return (int)(formattedText.Height * 2);
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

            var bounds = BannerGeometry.ToDeviceIndependentBounds(_grantedRect, scaleX, scaleY);

            this.Left = bounds.Left;
            this.Top = bounds.Top;
            this.Width = bounds.Width;
            this.Height = bounds.Height;
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
            _textRefreshTimer?.Stop();

            if (_hwnd != IntPtr.Zero)
            {
                AppBarHelper.UnregisterBar(_hwnd);
            }
        }
    }
}
