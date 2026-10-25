using System;
using ClassificationBanner;
using Xunit;

namespace ClassificationBanner.Tests
{
    /// <summary>
    /// Fixed system values so token expansion is verified independently of the machine
    /// the tests happen to run on.
    /// </summary>
    internal sealed class FakeSystemInfo : ISystemInfoProvider
    {
        public string HostName { get; set; } = "WS-12345";
        public string UserName { get; set; } = "jdoe";
        public string DomainName { get; set; } = "CONTOSO";
        public string IpAddress { get; set; } = "10.1.2.3";
        public string MacAddress { get; set; } = "00:1A:2B:3C:4D:5E";
        public DateTime Now { get; set; } = new DateTime(2024, 3, 7, 14, 5, 0);
    }

    /// <summary>
    /// Verifies what text an operator actually sees in a banner cell for a given template.
    /// </summary>
    public class BannerTextResolverTests
    {
        private static readonly FakeSystemInfo Info = new FakeSystemInfo();

        [Fact]
        public void PlainTextIsShownUnchanged()
        {
            Assert.Equal("SECRET // NOFORN", BannerTextResolver.Resolve("SECRET // NOFORN", Info));
        }

        [Theory]
        [InlineData("{hostname}", "WS-12345")]
        [InlineData("{ipaddress}", "10.1.2.3")]
        [InlineData("{username}", "jdoe")]
        [InlineData("{domain}", "CONTOSO")]
        [InlineData("{macaddress}", "00:1A:2B:3C:4D:5E")]
        [InlineData("{date}", "2024-03-07")]
        [InlineData("{time}", "14:05")]
        [InlineData("{datetime}", "2024-03-07 14:05")]
        public void EachSupportedTokenExpandsToItsValue(string template, string expected)
        {
            Assert.Equal(expected, BannerTextResolver.Resolve(template, Info));
        }

        [Theory]
        [InlineData("{HOSTNAME}")]
        [InlineData("{HostName}")]
        [InlineData("{ hostname }")]
        public void TokenNamesAreMatchedWithoutRegardToCaseOrPadding(string template)
        {
            Assert.Equal("WS-12345", BannerTextResolver.Resolve(template, Info));
        }

        [Fact]
        public void TokensAreExpandedWithinSurroundingText()
        {
            Assert.Equal(
                "Host: WS-12345 (10.1.2.3)",
                BannerTextResolver.Resolve("Host: {hostname} ({ipaddress})", Info));
        }

        [Fact]
        public void RepeatedTokensAreEachExpanded()
        {
            Assert.Equal("WS-12345/WS-12345", BannerTextResolver.Resolve("{hostname}/{hostname}", Info));
        }

        [Fact]
        public void AnUnknownTokenIsLeftVisibleRatherThanBlanked()
        {
            // A typo must be obvious on screen, not silently erase part of the marking.
            Assert.Equal("{hostnam}", BannerTextResolver.Resolve("{hostnam}", Info));
        }

        [Fact]
        public void AnUnterminatedBraceIsShownVerbatim()
        {
            Assert.Equal("SECRET {hostname", BannerTextResolver.Resolve("SECRET {hostname", Info));
        }

        [Fact]
        public void DoubledBracesProduceALiteralBrace()
        {
            Assert.Equal("{hostname}", BannerTextResolver.Resolve("{{hostname}}", Info));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void BlankTemplatesProduceNoText(string? template)
        {
            Assert.Equal(string.Empty, BannerTextResolver.Resolve(template, Info));
        }

        [Fact]
        public void AnEmptySystemValueExpandsToNothing()
        {
            // Offline machines have no IP address; the cell should simply be blank.
            var offline = new FakeSystemInfo { IpAddress = string.Empty };

            Assert.Equal("IP: ", BannerTextResolver.Resolve("IP: {ipaddress}", offline));
        }

        [Fact]
        public void ResolveRequiresASystemInfoProvider()
        {
            Assert.Throws<ArgumentNullException>(() => BannerTextResolver.Resolve("{hostname}", null!));
        }

        [Fact]
        public void EverySupportedTokenIsAdvertisedAndResolvable()
        {
            foreach (string token in BannerTextResolver.SupportedTokens)
            {
                string resolved = BannerTextResolver.Resolve("{" + token + "}", Info);
                Assert.False(resolved.StartsWith("{"), $"Token '{token}' is advertised but not resolved.");
            }
        }

        [Theory]
        [InlineData("{time}")]
        [InlineData("{date}")]
        [InlineData("{datetime}")]
        [InlineData("{ipaddress}")]
        [InlineData("{macaddress}")]
        [InlineData("Updated {TIME}")]
        public void TextThatChangesOverTimeIsMarkedForRefresh(string template)
        {
            Assert.True(BannerTextResolver.ContainsVolatileToken(template));
        }

        [Theory]
        [InlineData("{hostname}")]
        [InlineData("{username}")]
        [InlineData("{domain}")]
        [InlineData("SECRET // NOFORN")]
        [InlineData(null)]
        [InlineData("")]
        public void StaticTextIsNotMarkedForRefresh(string? template)
        {
            Assert.False(BannerTextResolver.ContainsVolatileToken(template));
        }
    }
}
