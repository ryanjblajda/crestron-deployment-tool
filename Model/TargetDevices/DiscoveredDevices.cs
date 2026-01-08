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
using System.Windows;
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
            AddDevice(new CrestronDevice("test device 1", "TS-1070", "192.168.1.1") { 
                IsSelected = true,
                DeploymentActions = { 
                    new DeviceDeployment.DeviceDeploymentAction("action 1", "action description 1"),
                    new DeviceDeployment.DeviceDeploymentAction("action 2", "action description 2"),
                    new DeviceDeployment.DeviceDeploymentAction("action 3", "action description 3"),
                    new DeviceDeployment.DeviceDeploymentAction("action 4", "action description 4"),
                    new DeviceDeployment.DeviceDeploymentAction("action 5", "action description 5"),
                } 
            }, SelectedTargetDevices);
            AddDevice(new CrestronDevice("test device 2", "CP4N", "192.168.1.2")
            {
                IsSelected = true,
                DeploymentActions = {
                    new DeviceDeployment.DeviceDeploymentAction("action 1", "action description 1"),
                    new DeviceDeployment.DeviceDeploymentAction("action 2", "action description 2"),
                    new DeviceDeployment.DeviceDeploymentAction("action 3", "action description 3"),
                    new DeviceDeployment.DeviceDeploymentAction("action 4", "action description 4"),
                    new DeviceDeployment.DeviceDeploymentAction("action 5", "action description 5"),
                }
            }, SelectedTargetDevices);
            AddDevice(new CrestronDevice("test device 3", "RMC3", "192.168.1.3")
            {
                IsSelected = true,
                DeploymentActions = {
                    new DeviceDeployment.DeviceDeploymentAction("action 1", "action description 1"),
                    new DeviceDeployment.DeviceDeploymentAction("action 2", "action description 2"),
                    new DeviceDeployment.DeviceDeploymentAction("action 3", "action description 3"),
                    new DeviceDeployment.DeviceDeploymentAction("action 4", "action description 4"),
                    new DeviceDeployment.DeviceDeploymentAction("action 5", "action description 5"),
                }
            }, SelectedTargetDevices);
            AddDevice(new CrestronDevice("test device 4", "TS-1060", "192.168.1.4")
            {
                IsSelected = true,
                DeploymentActions = {
                    new DeviceDeployment.DeviceDeploymentAction("action 1", "action description 1"),
                    new DeviceDeployment.DeviceDeploymentAction("action 2", "action description 2"),
                    new DeviceDeployment.DeviceDeploymentAction("action 3", "action description 3"),
                    new DeviceDeployment.DeviceDeploymentAction("action 4", "action description 4"),
                    new DeviceDeployment.DeviceDeploymentAction("action 5", "action description 5"),
                }
            }, SelectedTargetDevices);
            AddDevice(new CrestronDevice("test device 5", "TS-1052", "192.168.1.5")
            {
                IsSelected = true,
                DeploymentActions = {
                    new DeviceDeployment.DeviceDeploymentAction("action 1", "action description 1"),
                    new DeviceDeployment.DeviceDeploymentAction("action 2", "action description 2"),
                    new DeviceDeployment.DeviceDeploymentAction("action 3", "action description 3"),
                    new DeviceDeployment.DeviceDeploymentAction("action 4", "action description 4"),
                    new DeviceDeployment.DeviceDeploymentAction("action 5", "action description 5"),
                }
            }, SelectedTargetDevices);
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
