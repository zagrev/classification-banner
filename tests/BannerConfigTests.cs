using System.Text.Json;
using ClassificationBanner;
using Xunit;

namespace ClassificationBanner.Tests
{
    /// <summary>
    /// Verifies the settings contract: the markings shown when nothing is configured, and
    /// that an operator-authored settings file is interpreted as written.
    /// </summary>
    public class BannerConfigTests
    {
        [Fact]
        public void WithoutConfiguration_TheLeastSensitiveMarkingIsShown()
        {
            var config = new BannerConfig();

            Assert.Equal("UNCLASSIFIED", config.ClassificationText);
        }

        [Fact]
        public void DefaultsProduceAVisibleBannerAndAWorkingEnforcementLoop()
        {
            var config = new BannerConfig();

            Assert.True(config.BannerHeight > 0);
            Assert.True(config.EnforcementIntervalMs > 0);
            Assert.False(string.IsNullOrWhiteSpace(config.BackgroundColorHex));
            Assert.False(string.IsNullOrWhiteSpace(config.ForegroundColorHex));
        }

        [Fact]
        public void EverySettingCanBeOverriddenFromFile()
        {
            string json = @"{
                ""ClassificationText"": ""SECRET // NOFORN"",
                ""BackgroundColorHex"": ""#FF0000FF"",
                ""ForegroundColorHex"": ""#FF000000"",
                ""BannerHeight"": 48,
                ""EnforcementIntervalMs"": 1000
            }";

            var config = JsonSerializer.Deserialize<BannerConfig>(json)!;

            Assert.Equal("SECRET // NOFORN", config.ClassificationText);
            Assert.Equal("#FF0000FF", config.BackgroundColorHex);
            Assert.Equal("#FF000000", config.ForegroundColorHex);
            Assert.Equal(48, config.BannerHeight);
            Assert.Equal(1000, config.EnforcementIntervalMs);
        }

        [Fact]
        public void AnEmptySettingsFileYieldsTheDefaults()
        {
            var config = JsonSerializer.Deserialize<BannerConfig>("{}")!;
            var defaults = new BannerConfig();

            Assert.Equal(defaults.ClassificationText, config.ClassificationText);
            Assert.Equal(defaults.BannerHeight, config.BannerHeight);
            Assert.Equal(defaults.EnforcementIntervalMs, config.EnforcementIntervalMs);
        }

        [Fact]
        public void SettingNamesAreMatchedWithoutRegardToCase()
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var config = JsonSerializer.Deserialize<BannerConfig>(
                @"{ ""classificationtext"": ""SECRET"" }", options)!;

            Assert.Equal("SECRET", config.ClassificationText);
        }
    }
}
