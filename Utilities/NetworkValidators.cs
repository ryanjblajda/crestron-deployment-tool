using Serilog;
using System.Net;
using System.Text.RegularExpressions;

namespace CrestronDeploymentTool.Utilities
{
    /// <summary>
    /// a static utility class to provide validation of entered network details
    /// </summary>
    public static class NetworkValidators
    {
        private const string prefix = "NetworkValidators |";

        private static readonly Regex ValidIPAddressPattern = new(@"[\d]+\.[\d]+\.[\d]+\.[\d]+");

        private static readonly Regex ValidHostnamePattern = new(@"^(?=.{1,253}$)(?!-)[A-Za-z0-9-]{1,63}(?<!-)(\.(?!-)[A-Za-z0-9-]{1,63}(?<!-))*$");

        public static string NormalizeIPAddress(string ip)
        {
            var parts = ip.Split('.');
            
            if (parts.Length != 4) { return ip; }

            return string.Join(".", parts.Select(p => int.Parse(p).ToString()));
        }

        /// <summary>
        /// checks if a string is a valid ip address
        /// </summary>
        /// <param name="value">the potential ip address</param>
        /// <returns>a bool representing whether the string can be a valid ip address</returns>
        public static bool IsValidIPAddress(string value)
        {
            if (!ValidIPAddressPattern.IsMatch(value)) 
            {
                Log.Information($"{prefix} {value} does not regex match");
                return false;
            }
            
            bool result = IPAddress.TryParse(value, out var ip);
            Log.Information($"{prefix} {value} is not valid ip address");
            
            return result && ip?.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
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
