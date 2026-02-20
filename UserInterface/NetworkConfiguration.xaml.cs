using CrestronDeploymentTool.Model.TargetDevices;
using CrestronDeploymentTool.Utilities;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CrestronDeploymentTool.UserInterface
{
    /// <summary>
    /// Interaction logic for NetworkConfiguration.xaml
    /// </summary>
    public partial class NetworkConfiguration : Window
    {
        public DeviceNetworkConfiguration Configuration = new DeviceNetworkConfiguration("", "");

        public NetworkConfiguration(string prompt, string title, DeviceNetworkConfiguration configuration)
        {
            InitializeComponent();
            TextHelpers.ParseFormattedText(prompt, this.Prompt);
            this.Title = title;

            this.EnableDHCP.IsChecked = configuration.DHCP;
            this.HostnameEntered.Text = configuration.Hostname;
            this.IPAddressEntered.Text = configuration.IPAddress;
            this.SubnetMaskEntered.Text = configuration.Netmask;
            this.DefaultGatewayEntered.Text = configuration.DefaultGateway;

            if (configuration.DnsServers.Count > 0) { this.PrimaryDNSEntered.Text = configuration.DnsServers[0]; }
            if (configuration.DnsServers.Count > 1) { this.SecondaryDNSEntered.Text = configuration.DnsServers[1]; }
        }

        private void SetValidEntryBackground(TextBox item, bool valid)
        {
            if (valid) { item.Background = new SolidColorBrush(Color.FromArgb(51, 00, 00, 00));  }
            else { item.Background = new SolidColorBrush(Colors.Red); }
        }

        private void CheckAllItemsValid()
        {
            if (EnableDHCP.IsChecked == false)
            {
                if ((NetworkValidators.IsValidIPAddress(IPAddressEntered.Text) || IPAddressEntered.Text == String.Empty) &&
                    (NetworkValidators.IsValidIPAddress(SubnetMaskEntered.Text) || IPAddressEntered.Text == String.Empty)  &&
                    (NetworkValidators.IsValidIPAddress(DefaultGatewayEntered.Text) || IPAddressEntered.Text == String.Empty)  &&
                    (NetworkValidators.IsValidIPAddress(PrimaryDNSEntered.Text) || IPAddressEntered.Text == String.Empty) &&
                    (NetworkValidators.IsValidIPAddress(SecondaryDNSEntered.Text) || IPAddressEntered.Text == String.Empty) &&
                    (NetworkValidators.IsValidHostname(HostnameEntered.Text) || IPAddressEntered.Text == String.Empty)) { ConfirmButton.IsEnabled = true; }
                else { ConfirmButton.IsEnabled = false; }
            }
            else
            {
                if (NetworkValidators.IsValidHostname(HostnameEntered.Text) || HostnameEntered.Text == String.Empty) { ConfirmButton.IsEnabled = true; }
                else { ConfirmButton.IsEnabled = false; }
            }
        }

        private void OnTextEntryChanged(object sender, TextChangedEventArgs e)
        {
            if (sender == HostnameEntered) { SetValidEntryBackground((TextBox)sender, NetworkValidators.IsValidHostname(((TextBox)sender).Text)); }
            else { SetValidEntryBackground((TextBox)sender, NetworkValidators.IsValidIPAddress(((TextBox)sender).Text)); }

            CheckAllItemsValid();
        }

        private void SetEnableStatusStaticNetworkItems(bool? enable)
        {
            if (enable != null)
            {
                IPAddressEntered.IsEnabled = (bool)enable;
                SubnetMaskEntered.IsEnabled = (bool)enable;
                DefaultGatewayEntered.IsEnabled = (bool)enable;
                PrimaryDNSEntered.IsEnabled = (bool)enable;
                SecondaryDNSEntered.IsEnabled = (bool)enable;
            }
        }

        private void OnDHCPEnabledChanged(object sender, RoutedEventArgs e)
        {
            SetEnableStatusStaticNetworkItems(!EnableDHCP.IsChecked);
            CheckAllItemsValid();
        }

        private void OnConfirmConfigurationClicked(object sender, RoutedEventArgs e)
        {
            if (this.EnableDHCP.IsChecked != null) { this.Configuration.DHCP = this.EnableDHCP.IsChecked.Value; } ;
            this.Configuration.Hostname = this.HostnameEntered.Text;
            this.Configuration.IPAddress = this.IPAddressEntered.Text;
            this.Configuration.Netmask = this.SubnetMaskEntered.Text;
            this.Configuration.DefaultGateway = this.DefaultGatewayEntered.Text;
            this.Configuration.DnsServers.Add(this.PrimaryDNSEntered.Text);
            this.Configuration.DnsServers.Add(this.SecondaryDNSEntered.Text);

            this.DialogResult = true;
            this.Close();
        }
    }
}
