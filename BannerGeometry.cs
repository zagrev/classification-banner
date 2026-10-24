using System;

namespace ClassificationBanner
{
    /// <summary>
    /// Pure geometry helpers for AppBar placement. Separated from the P/Invoke surface so
    /// the rectangle arithmetic that governs banner position can be verified directly.
    /// </summary>
    public static class BannerGeometry
    {
        /// <summary>
        /// Converts a device-independent banner height into physical pixels, clamped to at
        /// least one pixel so a zero or negative configured height still renders.
        /// </summary>
        public static int ComputeThickness(int bannerHeightDips, double scale)
        {
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
            {
                scale = 1.0;
            }

            int thickness = (int)Math.Round(bannerHeightDips * scale);
            return thickness < 1 ? 1 : thickness;
        }

        /// <summary>
        /// Builds the rectangle requested from the shell for a screen edge. The full screen
        /// width is requested; the shell trims it during negotiation.
        /// </summary>
        public static AppBarHelper.RECT BuildDesiredRect(
            int screenLeft, int screenTop, int screenRight, int screenBottom,
            AppBarHelper.ABEdge edge, int thickness)
        {
            return new AppBarHelper.RECT
            {
                Left = screenLeft,
                Right = screenRight,
                Top = edge == AppBarHelper.ABEdge.ABE_TOP ? screenTop : screenBottom - thickness,
                Bottom = edge == AppBarHelper.ABEdge.ABE_TOP ? screenTop + thickness : screenBottom
            };
        }

        /// <summary>
        /// Re-derives the edge opposite the docking edge after the shell has adjusted the
        /// docking edge, preserving the intended thickness. ABM_QUERYPOS moves only the
        /// docking edge, leaving the opposite edge at its original value.
        /// </summary>
        public static AppBarHelper.RECT ApplyThickness(AppBarHelper.RECT rect, AppBarHelper.ABEdge edge, int thickness)
        {
            switch (edge)
            {
                case AppBarHelper.ABEdge.ABE_TOP:
                    rect.Bottom = rect.Top + thickness;
                    break;
                case AppBarHelper.ABEdge.ABE_BOTTOM:
                    rect.Top = rect.Bottom - thickness;
                    break;
                case AppBarHelper.ABEdge.ABE_LEFT:
                    rect.Right = rect.Left + thickness;
                    break;
                case AppBarHelper.ABEdge.ABE_RIGHT:
                    rect.Left = rect.Right - thickness;
                    break;
            }

            return rect;
        }

        /// <summary>
        /// Converts a physical-pixel rectangle into the device-independent bounds used by
        /// WPF's managed layout properties.
        /// </summary>
        public static (double Left, double Top, double Width, double Height) ToDeviceIndependentBounds(
            AppBarHelper.RECT rect, double scaleX, double scaleY)
        {
            if (double.IsNaN(scaleX) || double.IsInfinity(scaleX) || scaleX <= 0) scaleX = 1.0;
            if (double.IsNaN(scaleY) || double.IsInfinity(scaleY) || scaleY <= 0) scaleY = 1.0;

            return (rect.Left / scaleX, rect.Top / scaleY, rect.Width / scaleX, rect.Height / scaleY);
        }
    }
}
