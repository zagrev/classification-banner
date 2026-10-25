using System.Text.Json;
using ClassificationBanner;
using Xunit;

namespace ClassificationBanner.Tests
{
    /// <summary>
    /// Verifies which text each banner edge displays, including the fallback that keeps
    /// settings files written before per-edge text was supported rendering unchanged.
    /// </summary>
    public class BannerEdgeConfigTests
    {
        [Fact]
        public void WithNoEdgeConfigured_BothBannersShowTheClassificationCentered()
        {
            var config = new BannerConfig { ClassificationText = "SECRET" };

            foreach (var edge in new[] { AppBarHelper.ABEdge.ABE_TOP, AppBarHelper.ABEdge.ABE_BOTTOM })
            {
                var resolved = config.GetEdge(edge);

                Assert.Equal("SECRET", resolved.Center);
                Assert.True(string.IsNullOrEmpty(resolved.Left));
                Assert.True(string.IsNullOrEmpty(resolved.Right));
            }
        }

        [Fact]
        public void TopAndBottomCanCarryDifferentText()
        {
            var config = new BannerConfig
            {
                Top = new BannerEdgeConfig { Center = "TOP SECRET" },
                Bottom = new BannerEdgeConfig { Center = "Handle via SCI channels" }
            };

            Assert.Equal("TOP SECRET", config.GetEdge(AppBarHelper.ABEdge.ABE_TOP).Center);
            Assert.Equal("Handle via SCI channels", config.GetEdge(AppBarHelper.ABEdge.ABE_BOTTOM).Center);
        }

        [Fact]
        public void AnEdgeCanCarryAllThreeCells()
        {
            var config = new BannerConfig
            {
                Top = new BannerEdgeConfig
                {
                    Left = "{hostname}",
                    Center = "SECRET",
                    Right = "{ipaddress}"
                }
            };

            var resolved = config.GetEdge(AppBarHelper.ABEdge.ABE_TOP);

            Assert.Equal("{hostname}", resolved.Left);
            Assert.Equal("SECRET", resolved.Center);
            Assert.Equal("{ipaddress}", resolved.Right);
        }

        [Fact]
        public void ConfiguringOnlyOneEdgeLeavesTheOtherOnTheClassification()
        {
            var config = new BannerConfig
            {
                ClassificationText = "CONFIDENTIAL",
                Top = new BannerEdgeConfig { Left = "{hostname}" }
            };

            Assert.Equal("{hostname}", config.GetEdge(AppBarHelper.ABEdge.ABE_TOP).Left);
            Assert.Equal("CONFIDENTIAL", config.GetEdge(AppBarHelper.ABEdge.ABE_BOTTOM).Center);
        }

        [Fact]
        public void AnEdgeWithOnlyBlankCellsFallsBackToTheClassification()
        {
            var config = new BannerConfig
            {
                ClassificationText = "SECRET",
                Top = new BannerEdgeConfig { Left = "  ", Center = "", Right = null }
            };

            Assert.Equal("SECRET", config.GetEdge(AppBarHelper.ABEdge.ABE_TOP).Center);
        }

        [Theory]
        [InlineData(null, null, null, true)]
        [InlineData("", "  ", null, true)]
        [InlineData("x", null, null, false)]
        [InlineData(null, "x", null, false)]
        [InlineData(null, null, "x", false)]
        public void AnEdgeIsEmptyOnlyWhenEveryCellIsBlank(string? l, string? c, string? r, bool expected)
        {
            var edge = new BannerEdgeConfig { Left = l, Center = c, Right = r };

            Assert.Equal(expected, edge.IsEmpty);
        }

        [Fact]
        public void PerEdgeTextCanBeAuthoredInTheSettingsFile()
        {
            string json = @"{
                ""ClassificationText"": ""SECRET"",
                ""TextRefreshIntervalSeconds"": 15,
                ""Top"": {
                    ""Left"": ""{hostname}"",
                    ""Center"": ""SECRET // NOFORN"",
                    ""Right"": ""{ipaddress}""
                },
                ""Bottom"": {
                    ""Center"": ""Authorized use only""
                }
            }";

            var config = JsonSerializer.Deserialize<BannerConfig>(json)!;

            Assert.Equal(15, config.TextRefreshIntervalSeconds);

            var top = config.GetEdge(AppBarHelper.ABEdge.ABE_TOP);
            Assert.Equal("{hostname}", top.Left);
            Assert.Equal("SECRET // NOFORN", top.Center);
            Assert.Equal("{ipaddress}", top.Right);

            var bottom = config.GetEdge(AppBarHelper.ABEdge.ABE_BOTTOM);
            Assert.Equal("Authorized use only", bottom.Center);
            Assert.True(string.IsNullOrEmpty(bottom.Left));
        }

        [Fact]
        public void ASettingsFileWithoutEdgeSectionsStillRenders()
        {
            // Backward compatibility with files written before this feature existed.
            var config = JsonSerializer.Deserialize<BannerConfig>(
                @"{ ""ClassificationText"": ""UNCLASSIFIED"" }")!;

            Assert.Equal("UNCLASSIFIED", config.GetEdge(AppBarHelper.ABEdge.ABE_TOP).Center);
            Assert.Equal("UNCLASSIFIED", config.GetEdge(AppBarHelper.ABEdge.ABE_BOTTOM).Center);
        }

        [Fact]
        public void TextRefreshDefaultsToAModestInterval()
        {
            Assert.True(new BannerConfig().TextRefreshIntervalSeconds > 0);
        }
    }
}
