using System;
using System.IO;

namespace ClassificationBanner
{
    /// <summary>
    /// Parses the application's command line. Kept free of UI and file-system access so
    /// the argument grammar can be verified in isolation.
    /// </summary>
    public static class CommandLineOptions
    {
        /// <summary>
        /// Extracts the settings file path from the command line. Accepts
        /// "--config &lt;path&gt;", "--config=&lt;path&gt;", the "-c" short form,
        /// or a single bare positional path. Returns null when unspecified or
        /// when the supplied value is blank or malformed.
        /// </summary>
        public static string? ParseConfigPath(string[]? args)
        {
            if (args == null)
            {
                return null;
            }

            for (int i = 0; i < args.Length; i++)
            {
                string? arg = args[i];

                if (string.IsNullOrEmpty(arg))
                {
                    continue;
                }

                if (arg.StartsWith("--config=", StringComparison.OrdinalIgnoreCase))
                {
                    return NormalizePath(arg.Substring("--config=".Length));
                }

                if (arg.StartsWith("-c=", StringComparison.OrdinalIgnoreCase))
                {
                    return NormalizePath(arg.Substring("-c=".Length));
                }

                bool isConfigSwitch =
                    string.Equals(arg, "--config", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "-c", StringComparison.OrdinalIgnoreCase);

                if (isConfigSwitch)
                {
                    // A trailing switch with no value is treated as unspecified.
                    return i + 1 < args.Length ? NormalizePath(args[i + 1]) : null;
                }

                // Bare positional path, e.g. launched via file association or shortcut
                if (!arg.StartsWith("-", StringComparison.Ordinal))
                {
                    return NormalizePath(arg);
                }
            }

            return null;
        }

        /// <summary>
        /// Trims surrounding quotes and whitespace and resolves the value to an absolute
        /// path against the current working directory. Returns null if the value is blank
        /// or cannot form a valid path.
        /// </summary>
        public static string? NormalizePath(string? candidate)
        {
            if (candidate == null)
            {
                return null;
            }

            candidate = candidate.Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(candidate))
            {
                return null;
            }

            try
            {
                // Resolve relative paths against the working directory, not the exe directory
                return Path.GetFullPath(candidate);
            }
            catch
            {
                // Malformed path (invalid characters, too long, etc.)
                return null;
            }
        }

        /// <summary>
        /// Builds the command line used for the startup registry entry, preserving any
        /// explicitly selected settings file so the choice survives a reboot.
        /// </summary>
        public static string BuildStartupCommand(string executablePath, string? configPathOverride)
        {
            if (string.IsNullOrEmpty(executablePath))
            {
                throw new ArgumentException("Executable path is required.", nameof(executablePath));
            }

            return string.IsNullOrEmpty(configPathOverride)
                ? $"\"{executablePath}\""
                : $"\"{executablePath}\" --config \"{configPathOverride}\"";
        }
    }
}
