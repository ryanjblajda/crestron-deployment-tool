using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using Serilog;

namespace CrestronDeploymentTool.Model.TargetDevices
{
    /// <summary>
    /// a static class representing the discovered devices attached to the host
    /// </summary>
    public static class DiscoveredDevices
    {
        private const string prefix = "DiscoveredDevices";

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

        public static readonly ImmutableList<string> AnyCrestronDevice = UserInterfaceCapableDevices.Concat(ProgrammingCapableDevices).ToImmutableList();
        public static ObservableCollection<CrestronDevice> AvailableDiscoveredDevices { get; }
        public static ObservableCollection<CrestronDevice> SelectedTargetDevices { get; }

        static DiscoveredDevices()
        {
            AvailableDiscoveredDevices = new ObservableCollection<CrestronDevice>();
            SelectedTargetDevices = new ObservableCollection<CrestronDevice>();
            AvailableDiscoveredDevices.CollectionChanged += OnDiscoveredDevicesChanged;

            if (DesignerProperties.GetIsInDesignMode(new DependencyObject())) { LoadDebugElements(); }
        }

        /// <summary>
        /// loads debugging elements for design time
        /// </summary>
        private static void LoadDebugElements()
        {
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
                    AddDevice(device, AvailableDiscoveredDevices);
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

            if (device != null)
            {
                if (device.IsSelected) { AddDevice(device, SelectedTargetDevices); }
                else { SelectedTargetDevices.Remove(device); }
            }
        }

        /// <summary>
        /// addes a device to the discovered device list, provided it did not already exist
        /// </summary>
        /// <param name="device">the new device</param>
        public static void AddDevice(CrestronDevice device, ObservableCollection<CrestronDevice> targetList)
        {
            if (!targetList.Any(d => d.IpAddress == device.IpAddress || d.Name == device.Name))
                {
                lock (targetList)
                {
                    targetList.Add(device);
                }
            }
        }
    }
}
