using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrestronDeploymentTool.Model.TargetDevices
{
    public class DeviceNetworkConfiguration
    {
        public readonly List<string> DnsServers = new List<string>();
        public string Hostname { get; internal set; }
        public string IPAddress { get; internal set; }
        public string Netmask { get; internal set; }
        public string DefaultGateway { get; internal set; }

        public DeviceNetworkConfiguration(string host, string ip) 
        {
            this.Hostname = host;
            this.IPAddress = ip;
            this.Netmask = String.Empty;
            this.DefaultGateway = String.Empty;
        }
    }
}
