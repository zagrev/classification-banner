using ClassificationBanner;
using Xunit;

namespace ClassificationBanner.Tests
{
    /// <summary>
    /// Verifies where banners end up on screen. These cases encode the positioning defects
    /// previously observed: a top banner offset from the screen edge, a bottom banner
    /// overlapping the notification area, and bars losing their thickness during
    /// negotiation with the shell.
    /// </summary>
    public class BannerGeometryTests
    {
        [Fact]
        public void AtStandardScaling_ThicknessMatchesTheConfiguredHeight()
        {
            Assert.Equal(30, BannerGeometry.ComputeThickness(30, 1.0));
        }

        [Theory]
        [InlineData(30, 1.25, 38)]
        [InlineData(30, 1.5, 45)]
        [InlineData(30, 2.0, 60)]
        [InlineData(35, 1.75, 61)]
        public void UnderDisplayScaling_ThicknessGrowsProportionally(int height, double scale, int expected)
        {
            Assert.Equal(expected, BannerGeometry.ComputeThickness(height, scale));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void NonPositiveConfiguredHeight_StillProducesAVisibleBanner(int height)
        {
            Assert.True(BannerGeometry.ComputeThickness(height, 1.0) >= 1);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void UnusableScaleFactor_FallsBackToUnscaledThickness(double scale)
        {
            Assert.Equal(30, BannerGeometry.ComputeThickness(30, scale));
        }

        [Fact]
        public void TopBanner_SitsFlushAgainstTheTopOfTheScreen()
        {
            var rect = BannerGeometry.BuildDesiredRect(0, 0, 1920, 1080, AppBarHelper.ABEdge.ABE_TOP, 30);

            Assert.Equal(0, rect.Top);
            Assert.Equal(30, rect.Bottom);
            Assert.Equal(30, rect.Height);
        }

        [Fact]
        public void BottomBanner_SitsFlushAgainstTheBottomOfTheScreen()
        {
            var rect = BannerGeometry.BuildDesiredRect(0, 0, 1920, 1080, AppBarHelper.ABEdge.ABE_BOTTOM, 30);

            Assert.Equal(1050, rect.Top);
            Assert.Equal(1080, rect.Bottom);
            Assert.Equal(30, rect.Height);
        }

        [Theory]
        [InlineData(AppBarHelper.ABEdge.ABE_TOP)]
        [InlineData(AppBarHelper.ABEdge.ABE_BOTTOM)]
        public void BannersSpanTheFullWidthOfTheirScreen(AppBarHelper.ABEdge edge)
        {
            var rect = BannerGeometry.BuildDesiredRect(0, 0, 1920, 1080, edge, 30);

            Assert.Equal(0, rect.Left);
            Assert.Equal(1920, rect.Right);
            Assert.Equal(1920, rect.Width);
        }

        [Theory]
        [InlineData(AppBarHelper.ABEdge.ABE_TOP)]
        [InlineData(AppBarHelper.ABEdge.ABE_BOTTOM)]
        public void OnASecondaryScreen_BannersUseThatScreensCoordinates(AppBarHelper.ABEdge edge)
        {
            // A monitor positioned to the left of, and above, the primary display.
            var rect = BannerGeometry.BuildDesiredRect(-1920, -200, 0, 880, edge, 30);

            Assert.Equal(-1920, rect.Left);
            Assert.Equal(0, rect.Right);
            Assert.Equal(30, rect.Height);
            Assert.True(rect.Top >= -200 && rect.Bottom <= 880);
        }

        [Fact]
        public void WhenTheShellPushesATopBarDown_ItKeepsItsFullThickness()
        {
            // The shell has moved the bar below a 40px region it already reserved.
            var negotiated = new AppBarHelper.RECT { Left = 0, Top = 40, Right = 1920, Bottom = 30 };

            var result = BannerGeometry.ApplyThickness(negotiated, AppBarHelper.ABEdge.ABE_TOP, 30);

            Assert.Equal(40, result.Top);
            Assert.Equal(70, result.Bottom);
            Assert.Equal(30, result.Height);
        }

        [Fact]
        public void WhenTheShellLiftsABottomBar_ItKeepsItsFullThickness()
        {
            // The shell has raised the bar above a 40px taskbar.
            var negotiated = new AppBarHelper.RECT { Left = 0, Top = 1050, Right = 1920, Bottom = 1040 };

            var result = BannerGeometry.ApplyThickness(negotiated, AppBarHelper.ABEdge.ABE_BOTTOM, 30);

            Assert.Equal(1010, result.Top);
            Assert.Equal(1040, result.Bottom);
            Assert.Equal(30, result.Height);
        }

        [Fact]
        public void ANegotiatedBottomBarDoesNotExtendBelowTheSpaceItWasGranted()
        {
            var negotiated = new AppBarHelper.RECT { Left = 0, Top = 1050, Right = 1920, Bottom = 1040 };

            var result = BannerGeometry.ApplyThickness(negotiated, AppBarHelper.ABEdge.ABE_BOTTOM, 30);

            Assert.True(result.Bottom <= 1040);
        }

        [Theory]
        [InlineData(AppBarHelper.ABEdge.ABE_LEFT, 100, 130)]
        [InlineData(AppBarHelper.ABEdge.ABE_RIGHT, 170, 200)]
        public void VerticalEdges_AlsoPreserveTheirThickness(AppBarHelper.ABEdge edge, int expectedLeft, int expectedRight)
        {
            var negotiated = new AppBarHelper.RECT { Left = 100, Top = 0, Right = 200, Bottom = 1080 };

            var result = BannerGeometry.ApplyThickness(negotiated, edge, 30);

            Assert.Equal(expectedLeft, result.Left);
            Assert.Equal(expectedRight, result.Right);
            Assert.Equal(30, result.Width);
        }

        [Fact]
        public void AtStandardScaling_LayoutBoundsMatchThePhysicalRectangle()
        {
            var rect = new AppBarHelper.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 30 };

            var bounds = BannerGeometry.ToDeviceIndependentBounds(rect, 1.0, 1.0);

            Assert.Equal(0, bounds.Left);
            Assert.Equal(0, bounds.Top);
            Assert.Equal(1920, bounds.Width);
            Assert.Equal(30, bounds.Height);
        }

        [Fact]
        public void UnderDisplayScaling_LayoutBoundsAreReducedToLogicalUnits()
        {
            var rect = new AppBarHelper.RECT { Left = 0, Top = 0, Right = 3840, Bottom = 60 };

            var bounds = BannerGeometry.ToDeviceIndependentBounds(rect, 2.0, 2.0);

            Assert.Equal(1920, bounds.Width);
            Assert.Equal(30, bounds.Height);
        }

        [Fact]
        public void LayoutBoundsPreserveNegativeOriginsOnSecondaryScreens()
        {
            var rect = new AppBarHelper.RECT { Left = -1920, Top = -200, Right = 0, Bottom = -170 };

            var bounds = BannerGeometry.ToDeviceIndependentBounds(rect, 1.0, 1.0);

            Assert.Equal(-1920, bounds.Left);
            Assert.Equal(-200, bounds.Top);
            Assert.Equal(1920, bounds.Width);
            Assert.Equal(30, bounds.Height);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(double.NaN, double.NaN)]
        [InlineData(-1, -1)]
        public void UnusableScaleFactors_LeaveLayoutBoundsUnscaled(double scaleX, double scaleY)
        {
            var rect = new AppBarHelper.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 30 };

            var bounds = BannerGeometry.ToDeviceIndependentBounds(rect, scaleX, scaleY);

            Assert.Equal(1920, bounds.Width);
            Assert.Equal(30, bounds.Height);
        }

        [Fact]
        public void AConfiguredBannerSurvivesTheFullPlacementPipelineIntact()
        {
            // 150% scaling, shell reserves 60 physical pixels at the bottom for the taskbar.
            int thickness = BannerGeometry.ComputeThickness(30, 1.5);

            var desired = BannerGeometry.BuildDesiredRect(0, 0, 2880, 1620, AppBarHelper.ABEdge.ABE_BOTTOM, thickness);
            var negotiated = desired;
            negotiated.Bottom = 1560;

            var granted = BannerGeometry.ApplyThickness(negotiated, AppBarHelper.ABEdge.ABE_BOTTOM, thickness);
            var bounds = BannerGeometry.ToDeviceIndependentBounds(granted, 1.5, 1.5);

            Assert.Equal(45, granted.Height);
            Assert.Equal(1560, granted.Bottom);
            Assert.Equal(30, bounds.Height);
            Assert.Equal(1920, bounds.Width);
        }
    }
}
