using CrestronDeploymentTool.Model.Deployment;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace CrestronDeploymentTool.Model.TargetDevices
{
    /// <summary>
    /// a static class representing the discovered devices attached to the host
    /// </summary>
    public static class DiscoveredDevices
    {
        public static readonly ImmutableList<string> ProgrammingCapableDevices = [
            "MC", 
            "CP",
            "PRO",
            "AV",
            "RMC",
            "DIN",
            "MPC",
            "DMPS3"
        ];

        public static readonly ImmutableList<string> UserInterfaceCapableDevices = [
            "TS-",   // Tabletop touch screens
            "TSW-",  // Wall mount touch screens
            "TST-",  // Wireless touch screens
            "TSS-",  // Scheduling touch panels
            "TPS-",  // Older Isys series touch panels
            "CT-",   // Early color touch screens
            "STS-"   // Early wireless SmarTouch panels
        ];

        public static ObservableCollection<CrestronDevice> AvailableDiscoveredDevices { get; }
        public static ObservableCollection<CrestronDevice> SelectedTargetDevices { get; }

        static DiscoveredDevices()
        {
            AvailableDiscoveredDevices = new ObservableCollection<CrestronDevice>();
            SelectedTargetDevices = new ObservableCollection<CrestronDevice>();
            AvailableDiscoveredDevices.CollectionChanged += OnDiscoveredDevicesChanged;
        }

        /// <summary>
        /// a callback for when the amount of discovered devices changes
        /// </summary>
        /// <param name="sender">the collection that sent the callback</param>
        /// <param name="e">the changes that have occured</param>
        private static void OnDiscoveredDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (CrestronDevice device in e.NewItems)
                {
                    device.PropertyChanged += OnDevicePropertyChanged;
                }
            }
        }

        /// <summary>
        /// a callback fired when a device property changes
        /// </summary>
        /// <param name="sender">the device that sent the change</param>
        /// <param name="e">the property that changed</param>
        private static void OnDevicePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            CrestronDevice? device = ((CrestronDevice?)sender);

            if (device != null) {
                if (device.IsSelected) { 
                    if (!SelectedTargetDevices.Contains(device)) { SelectedTargetDevices.Add(device); } 
                }
                else { SelectedTargetDevices.Remove(device); }
            }
        }

        /// <summary>
        /// addes a device to the discovered device list, provided it did not already exist
        /// </summary>
        /// <param name="device">the new device</param>
        public static void AddDevice(CrestronDevice device)
        {
            if (!AvailableDiscoveredDevices.Any(d => d.IpAddress == device.IpAddress || d.Name == device.Name))
            {
                lock (AvailableDiscoveredDevices)
                {
                    AvailableDiscoveredDevices.Add(device);
                }
            }
        }
    }
}
