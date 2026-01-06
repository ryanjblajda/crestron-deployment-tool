using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace CrestronDeploymentTool.Model.Networking
{
    /// <summary>
    /// a static class representing the available network interfaces that could discover crestron devices
    /// </summary>
    public static class AvailableNetworkInterfaces
    {
        private const string prefix = "AvailableNetworkInterfaces |";
        public static ObservableCollection<AvailableNetworkInterface> Interfaces { get; } = new ObservableCollection<AvailableNetworkInterface>();

        static AvailableNetworkInterfaces()
        {
            if (DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject())) { LoadDebugElements(); }
        }

        /// <summary>
        /// gets availalable network interfaces on the host that could support discovering crestron devices.
        /// </summary>
        public static void GetAvailableNetworkInterfaces()
        {
            NetworkInterface[] availableNICs = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface nic in availableNICs)
            {
                Debug.WriteLine($"{prefix} {nic.Name} -- Supports IPV4?: {nic.Supports(NetworkInterfaceComponent.IPv4)}");
                List<string> addresses = nic.GetIPProperties().UnicastAddresses.Where(ip => ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).Select(ip => ip.Address.ToString()).ToList();
                if (nic.Supports(NetworkInterfaceComponent.IPv4) && addresses.Count > 0) { AddInterface(new AvailableNetworkInterface(false, nic.Name, addresses.First(), nic)); }
            }
        }

        /// <summary>
        /// loads debug elements for design time
        /// </summary>
        private static void LoadDebugElements()
        {
            new List<string>() { "Ethernet", "WiFi" }.ForEach(item => { Interfaces.Add(new AvailableNetworkInterface(false, item)); });
        }

        /// <summary>
        /// adds a network interface to the list, after making sure it doesnt already exist in the list
        /// </summary>
        /// <param name="intf">the interface to add</param>
        /// <returns>a bool representing whether the interface was added to the list</returns>
        public static bool AddInterface(AvailableNetworkInterface intf)
        {
            bool result = false;

            if (!Interfaces.ToList().Any(i => i.Name == intf.Name))
            {
                result = true;
                Interfaces.Add(intf);
            }

            return result;
        }
    }
}
