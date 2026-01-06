using System.Net;
using System.Text.RegularExpressions;

namespace CrestronDeploymentTool.Utilities
{
    /// <summary>
    /// a static utility class to provide validation of entered network details
    /// </summary>
    public static class NetworkValidators
    {
        private static readonly Regex ValidIPAddressPattern = new(@"^\d{1,3}(\.\d{1,3}){3}$");

        private static readonly Regex ValidHostnamePattern = new(@"^(?=.{1,253}$)(?!-)[A-Za-z0-9-]{1,63}(?<!-)(\.(?!-)[A-Za-z0-9-]{1,63}(?<!-))*$");

        /// <summary>
        /// checks if a string is a valid ip address
        /// </summary>
        /// <param name="value">the potential ip address</param>
        /// <returns>a bool representing whether the string can be a valid ip address</returns>
        public static bool IsValidIPAddress(string value)
        {
            if (!ValidIPAddressPattern.IsMatch(value)) { return false; }

            return IPAddress.TryParse(value, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
        }

        /// <summary>
        /// checks if a string is a valid hostname
        /// </summary>
        /// <param name="value">the potential hostname</param>
        /// <returns>a bool representing whether the string can be a valid hostname</returns>
        public static bool IsValidHostname(string value) => ValidHostnamePattern.IsMatch(value);

        /// <summary>
        /// checks if a string is a valid ip address or hostname
        /// </summary>
        /// <param name="value">the potential ip address or hostname</param>
        /// <returns>a bool representing if the string can be a valid hostname or ip address</returns>
        public static bool IsValidIPAddressOrHostname(string value) => IsValidIPAddress(value) || IsValidHostname(value);
    }
}
