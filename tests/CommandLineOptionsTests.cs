using System;
using System.IO;
using ClassificationBanner;
using Xunit;

namespace ClassificationBanner.Tests
{
    /// <summary>
    /// Verifies the command line grammar: which settings file the application will use for
    /// a given invocation, and what it records so the choice survives a reboot.
    /// </summary>
    public class CommandLineOptionsTests
    {
        [Fact]
        public void NoArguments_SelectsNoOverride()
        {
            Assert.Null(CommandLineOptions.ParseConfigPath(new string[0]));
        }

        [Fact]
        public void NullArguments_SelectsNoOverride()
        {
            Assert.Null(CommandLineOptions.ParseConfigPath(null));
        }

        [Fact]
        public void EmptyArgumentsAreIgnoredRatherThanTreatedAsAPath()
        {
            string expected = Path.GetFullPath(@"C:\settings\secret.json");

            string? actual = CommandLineOptions.ParseConfigPath(
                new[] { "", "--config", @"C:\settings\secret.json" });

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void OnlyEmptyArguments_SelectsNoOverride()
        {
            Assert.Null(CommandLineOptions.ParseConfigPath(new[] { "", "" }));
        }

        [Theory]
        [InlineData("--config")]
        [InlineData("-c")]
        [InlineData("--CONFIG")]
        [InlineData("-C")]
        public void SeparateValueForm_SelectsTheRequestedFile(string option)
        {
            string expected = Path.GetFullPath(@"C:\settings\secret.json");

            string? actual = CommandLineOptions.ParseConfigPath(
                new[] { option, @"C:\settings\secret.json" });

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(@"--config=C:\settings\secret.json")]
        [InlineData(@"-c=C:\settings\secret.json")]
        [InlineData(@"--CONFIG=C:\settings\secret.json")]
        public void InlineValueForm_SelectsTheRequestedFile(string argument)
        {
            string expected = Path.GetFullPath(@"C:\settings\secret.json");

            Assert.Equal(expected, CommandLineOptions.ParseConfigPath(new[] { argument }));
        }

        [Fact]
        public void BarePositionalPath_IsTreatedAsTheSettingsFile()
        {
            string expected = Path.GetFullPath(@"C:\settings\secret.json");

            Assert.Equal(expected, CommandLineOptions.ParseConfigPath(
                new[] { @"C:\settings\secret.json" }));
        }

        [Fact]
        public void RelativePath_IsResolvedToAnAbsolutePath()
        {
            string? actual = CommandLineOptions.ParseConfigPath(new[] { "--config", "secret.json" });

            Assert.NotNull(actual);
            Assert.True(Path.IsPathRooted(actual));
            Assert.EndsWith("secret.json", actual);
        }

        [Fact]
        public void QuotedAndPaddedValue_HasSurroundingNoiseRemoved()
        {
            string expected = Path.GetFullPath(@"C:\settings\secret.json");

            Assert.Equal(expected, CommandLineOptions.ParseConfigPath(
                new[] { "--config", "  \"C:\\settings\\secret.json\"  " }));
        }

        [Fact]
        public void TrailingSwitchWithNoValue_SelectsNoOverride()
        {
            Assert.Null(CommandLineOptions.ParseConfigPath(new[] { "--config" }));
        }

        [Fact]
        public void BlankValue_SelectsNoOverride()
        {
            Assert.Null(CommandLineOptions.ParseConfigPath(new[] { "--config", "   " }));
        }

        [Fact]
        public void UnrecognizedSwitches_AreSkippedInFavourOfTheConfigSwitch()
        {
            string expected = Path.GetFullPath(@"C:\settings\secret.json");

            string? actual = CommandLineOptions.ParseConfigPath(
                new[] { "--verbose", "--config", @"C:\settings\secret.json" });

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void UnrecognizedSwitchAlone_SelectsNoOverride()
        {
            Assert.Null(CommandLineOptions.ParseConfigPath(new[] { "--verbose" }));
        }

        [Fact]
        public void MalformedPath_SelectsNoOverrideRatherThanThrowing()
        {
            string? actual = CommandLineOptions.ParseConfigPath(new[] { "--config", "C:\\a\0b.json" });

            Assert.Null(actual);
        }

        [Fact]
        public void NormalizePath_ReturnsNullForNull()
        {
            Assert.Null(CommandLineOptions.NormalizePath(null));
        }

        [Fact]
        public void StartupCommand_WithoutOverride_LaunchesTheExecutableAlone()
        {
            string actual = CommandLineOptions.BuildStartupCommand(@"C:\app\banner.exe", null);

            Assert.Equal("\"C:\\app\\banner.exe\"", actual);
        }

        [Fact]
        public void StartupCommand_WithOverride_PreservesTheSettingsFileChoice()
        {
            string actual = CommandLineOptions.BuildStartupCommand(
                @"C:\app\banner.exe", @"C:\settings\secret.json");

            Assert.Equal("\"C:\\app\\banner.exe\" --config \"C:\\settings\\secret.json\"", actual);
        }

        [Fact]
        public void StartupCommand_RoundTripsBackToTheSameSettingsFile()
        {
            string configPath = Path.GetFullPath(@"C:\settings\my settings.json");

            string command = CommandLineOptions.BuildStartupCommand(@"C:\app\banner.exe", configPath);

            // Emulate the shell splitting the recorded command line back into arguments.
            int switchIndex = command.IndexOf("--config", StringComparison.Ordinal);
            string recordedValue = command.Substring(switchIndex + "--config".Length).Trim();

            Assert.Equal(configPath, CommandLineOptions.ParseConfigPath(new[] { "--config", recordedValue }));
        }

        [Fact]
        public void StartupCommand_RequiresAnExecutablePath()
        {
            Assert.Throws<ArgumentException>(() => CommandLineOptions.BuildStartupCommand("", null));
        }
    }
}
