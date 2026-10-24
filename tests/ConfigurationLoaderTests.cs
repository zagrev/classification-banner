using System;
using System.IO;
using ClassificationBanner;
using Xunit;

namespace ClassificationBanner.Tests
{
    /// <summary>
    /// Verifies which configuration the application ends up running with, and — critically
    /// for a classification-marking tool — when it refuses to start rather than display
    /// markings the operator did not ask for.
    /// </summary>
    public class ConfigurationLoaderTests
    {
        private const string ValidJson = @"{
            ""ClassificationText"": ""TOP SECRET // SCI"",
            ""BackgroundColorHex"": ""#FFC80000"",
            ""ForegroundColorHex"": ""#FFFFFFFF"",
            ""BannerHeight"": 35,
            ""EnforcementIntervalMs"": 250
        }";

        private static ConfigurationLoader Loader(bool exists, string content = "") =>
            new ConfigurationLoader(_ => exists, _ => content);

        private static ConfigurationLoader Throwing(bool exists) =>
            new ConfigurationLoader(_ => exists, _ => throw new IOException("locked"));

        [Fact]
        public void ValidFile_IsLoaded()
        {
            var result = Loader(true, ValidJson).Load(@"C:\settings\secret.json");

            Assert.Equal(ConfigLoadStatus.Loaded, result.Status);
            Assert.False(result.IsFatal);
            Assert.Equal("TOP SECRET // SCI", result.Config.ClassificationText);
            Assert.Equal(35, result.Config.BannerHeight);
            Assert.Equal(250, result.Config.EnforcementIntervalMs);
        }

        [Fact]
        public void PartialFile_LeavesUnspecifiedSettingsAtTheirDefaults()
        {
            var result = Loader(true, @"{ ""ClassificationText"": ""SECRET"" }").Load(null, @"C:\app\appsettings.json");

            Assert.Equal(ConfigLoadStatus.Loaded, result.Status);
            Assert.Equal("SECRET", result.Config.ClassificationText);
            Assert.Equal(new BannerConfig().BannerHeight, result.Config.BannerHeight);
        }

        [Fact]
        public void ExplicitFileMissing_AbortsStartup()
        {
            var result = Loader(false).Load(@"C:\settings\missing.json");

            Assert.Equal(ConfigLoadStatus.Fatal, result.Status);
            Assert.True(result.IsFatal);
            Assert.Contains("missing.json", result.ErrorMessage);
        }

        [Fact]
        public void ExplicitFileMalformed_AbortsStartup()
        {
            var result = Loader(true, "{ this is not json").Load(@"C:\settings\bad.json");

            Assert.Equal(ConfigLoadStatus.Fatal, result.Status);
            Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        }

        [Fact]
        public void ExplicitFileContainingJsonNull_AbortsStartup()
        {
            var result = Loader(true, "null").Load(@"C:\settings\null.json");

            Assert.Equal(ConfigLoadStatus.Fatal, result.Status);
        }

        [Fact]
        public void ExplicitFileUnreadable_AbortsStartup()
        {
            var result = Throwing(true).Load(@"C:\settings\locked.json");

            Assert.Equal(ConfigLoadStatus.Fatal, result.Status);
            Assert.Contains("locked", result.ErrorMessage);
        }

        [Fact]
        public void DefaultFileMissing_FallsBackToDefaultsAndStarts()
        {
            var result = Loader(false).Load(null, @"C:\app\appsettings.json");

            Assert.Equal(ConfigLoadStatus.DefaultsUsed, result.Status);
            Assert.False(result.IsFatal);
            Assert.Equal(new BannerConfig().ClassificationText, result.Config.ClassificationText);
        }

        [Fact]
        public void DefaultFileMalformed_FallsBackToDefaultsAndStarts()
        {
            var result = Loader(true, "<not json>").Load(null, @"C:\app\appsettings.json");

            Assert.Equal(ConfigLoadStatus.DefaultsUsed, result.Status);
            Assert.False(result.IsFatal);
        }

        [Fact]
        public void DefaultFileUnreadable_FallsBackToDefaultsAndStarts()
        {
            var result = Throwing(true).Load(null, @"C:\app\appsettings.json");

            Assert.Equal(ConfigLoadStatus.DefaultsUsed, result.Status);
        }

        [Fact]
        public void DefaultFileContainingJsonNull_FallsBackToDefaultsAndStarts()
        {
            var result = Loader(true, "null").Load(null, @"C:\app\appsettings.json");

            Assert.Equal(ConfigLoadStatus.DefaultsUsed, result.Status);
        }

        [Fact]
        public void OverridePath_TakesPrecedenceOverTheDefaultFile()
        {
            string? requested = null;
            var loader = new ConfigurationLoader(
                p => { requested = p; return true; },
                _ => ValidJson);

            loader.Load(@"C:\settings\secret.json", @"C:\app\appsettings.json");

            Assert.Equal(@"C:\settings\secret.json", requested);
        }

        [Fact]
        public void WithoutOverride_TheDefaultFileIsUsed()
        {
            string? requested = null;
            var loader = new ConfigurationLoader(
                p => { requested = p; return true; },
                _ => ValidJson);

            loader.Load(null, @"C:\app\appsettings.json");

            Assert.Equal(@"C:\app\appsettings.json", requested);
        }

        [Fact]
        public void ReportedPath_IdentifiesTheFileThatWasAttempted()
        {
            var result = Loader(false).Load(@"C:\settings\missing.json");

            Assert.Equal(@"C:\settings\missing.json", result.Path);
        }

        [Fact]
        public void DefaultConfigPath_ResolvesBesideTheExecutable()
        {
            Assert.EndsWith("appsettings.json", ConfigurationLoader.DefaultConfigPath);
            Assert.True(Path.IsPathRooted(ConfigurationLoader.DefaultConfigPath));
        }

        [Fact]
        public void SuccessfulLoad_ReportsNoError()
        {
            var result = Loader(true, ValidJson).Load(@"C:\settings\secret.json");

            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public void Constructor_RejectsMissingDependencies()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigurationLoader(null!, _ => ""));
            Assert.Throws<ArgumentNullException>(() => new ConfigurationLoader(_ => true, null!));
        }

        [Fact]
        public void DefaultLoader_ReadsAnActualSettingsFileFromDisk()
        {
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
            File.WriteAllText(path, ValidJson);

            try
            {
                var result = new ConfigurationLoader().Load(path);

                Assert.Equal(ConfigLoadStatus.Loaded, result.Status);
                Assert.Equal("TOP SECRET // SCI", result.Config.ClassificationText);
                Assert.Equal(35, result.Config.BannerHeight);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void DefaultLoader_AbortsWhenAnExplicitFileIsGenuinelyAbsent()
        {
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");

            var result = new ConfigurationLoader().Load(path);

            Assert.Equal(ConfigLoadStatus.Fatal, result.Status);
        }
    }
}
