using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace CrestronDeploymentTool.Model.Networking
{
   /// <summary>
   /// a class to represent the network interface and its status for utilization of crestron device discovery
   /// </summary>
   public class AvailableNetworkInterface : INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }
        public string Name { get; private set; }
        public string Address { get; private set; }
        public NetworkInterface? Interface { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public AvailableNetworkInterface() 
        {
            this.Name = "Waiting...";
            this.Address = "???.???.???.???";
        }

        public AvailableNetworkInterface(bool isSelected, string name) : this()
        {
            IsSelected = isSelected;
            Name = name;
        }
        public AvailableNetworkInterface(bool isSelected, string name, string address, NetworkInterface intf) : this()
        {
            IsSelected = isSelected;
            Name = name;
            Address = address;
            Interface = intf;
        }
    }
}
