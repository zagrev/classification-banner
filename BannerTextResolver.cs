using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace ClassificationBanner
{
    /// <summary>
    /// Supplies the live system values that banner tokens expand to. Injectable so token
    /// expansion can be verified without depending on the host's real identity or network.
    /// </summary>
    public interface ISystemInfoProvider
    {
        string HostName { get; }
        string UserName { get; }
        string DomainName { get; }
        string IpAddress { get; }
        string MacAddress { get; }
        DateTime Now { get; }
    }

    /// <summary>
    /// Reads system values from the running machine. Excluded from coverage because every
    /// member queries live operating system and network hardware state; the token
    /// expansion that consumes these values is covered via <see cref="ISystemInfoProvider"/>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public sealed class SystemInfoProvider : ISystemInfoProvider
    {
        public string HostName
        {
            get
            {
                try { return Environment.MachineName; }
                catch { return string.Empty; }
            }
        }

        public string UserName
        {
            get
            {
                try { return Environment.UserName; }
                catch { return string.Empty; }
            }
        }

        public string DomainName
        {
            get
            {
                try { return Environment.UserDomainName; }
                catch { return string.Empty; }
            }
        }

        /// <summary>
        /// The IPv4 address of the first operational, non-loopback interface. Returns an
        /// empty string when the machine is offline, which is common on the air-gapped
        /// workstations this application targets.
        /// </summary>
        public string IpAddress
        {
            get
            {
                try
                {
                    var candidate = NetworkInterface.GetAllNetworkInterfaces()
                        .Where(n => n.OperationalStatus == OperationalStatus.Up)
                        .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                        .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                        .Select(a => a.Address)
                        .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork
                                          && !IPAddress.IsLoopback(a));

                    return candidate?.ToString() ?? string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        /// <summary>
        /// The MAC address of the first operational, non-loopback interface, formatted
        /// with colon separators.
        /// </summary>
        public string MacAddress
        {
            get
            {
                try
                {
                    var nic = NetworkInterface.GetAllNetworkInterfaces()
                        .Where(n => n.OperationalStatus == OperationalStatus.Up)
                        .FirstOrDefault(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                    if (nic == null) return string.Empty;

                    byte[] bytes = nic.GetPhysicalAddress().GetAddressBytes();
                    if (bytes.Length == 0) return string.Empty;

                    return string.Join(":", bytes.Select(b => b.ToString("X2")));
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        public DateTime Now => DateTime.Now;
    }

    /// <summary>
    /// Expands tokens such as "{hostname}" within banner text. Unknown tokens are left
    /// intact so a typo is visible on screen rather than silently blanking the cell, which
    /// matters when the banner carries classification markings.
    /// </summary>
    public static class BannerTextResolver
    {
        /// <summary>
        /// Token names recognised by <see cref="Resolve"/>, for documentation and
        /// diagnostics. Matching is case-insensitive.
        /// </summary>
        public static readonly string[] SupportedTokens =
        {
            "hostname",
            "ipaddress",
            "username",
            "domain",
            "macaddress",
            "date",
            "time",
            "datetime"
        };

        /// <summary>
        /// Tokens whose value can change while the application is running, and which
        /// therefore justify periodic re-evaluation. Hostname, user, and domain are fixed
        /// for the life of a session and are deliberately excluded.
        /// </summary>
        private static readonly string[] VolatileTokens =
        {
            "ipaddress",
            "macaddress",
            "date",
            "time",
            "datetime"
        };

        /// <summary>
        /// True when the template contains at least one token whose value changes over
        /// time, so a banner showing only static text is never needlessly refreshed.
        /// </summary>
        public static bool ContainsVolatileToken(string? template)
        {
            if (string.IsNullOrWhiteSpace(template)) return false;

            foreach (string token in VolatileTokens)
            {
                if (template!.IndexOf("{" + token + "}", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Replaces every recognised "{token}" in <paramref name="template"/> with its
        /// current value. Returns an empty string for null or blank input. Unrecognised
        /// tokens are preserved verbatim. A literal brace can be written "{{" or "}}".
        /// </summary>
        public static string Resolve(string? template, ISystemInfoProvider info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            if (string.IsNullOrWhiteSpace(template)) return string.Empty;

            var result = new StringBuilder(template!.Length);

            for (int i = 0; i < template.Length; i++)
            {
                char current = template[i];

                // Escaped braces: "{{" and "}}" emit a single literal brace.
                if ((current == '{' || current == '}')
                    && i + 1 < template.Length
                    && template[i + 1] == current)
                {
                    result.Append(current);
                    i++;
                    continue;
                }

                if (current != '{')
                {
                    result.Append(current);
                    continue;
                }

                int close = template.IndexOf('}', i + 1);
                if (close < 0)
                {
                    // Unterminated brace; emit the remainder verbatim.
                    result.Append(template, i, template.Length - i);
                    break;
                }

                string token = template.Substring(i + 1, close - i - 1);
                string? value = Lookup(token, info);

                // Unknown tokens are preserved, braces included, so the mistake is visible.
                result.Append(value ?? template.Substring(i, close - i + 1));
                i = close;
            }

            return result.ToString();
        }

        private static string? Lookup(string token, ISystemInfoProvider info)
        {
            switch (token.Trim().ToLowerInvariant())
            {
                case "hostname": return info.HostName;
                case "ipaddress": return info.IpAddress;
                case "username": return info.UserName;
                case "domain": return info.DomainName;
                case "macaddress": return info.MacAddress;
                case "date": return info.Now.ToString("yyyy-MM-dd");
                case "time": return info.Now.ToString("HH:mm");
                case "datetime": return info.Now.ToString("yyyy-MM-dd HH:mm");
                default: return null;
            }
        }
    }
}
