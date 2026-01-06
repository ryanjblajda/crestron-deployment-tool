using System.Drawing;
using System.Windows;
using System.Windows.Media;

namespace CrestronDeploymentTool.UserInterface
{
    /// <summary>
    /// Interaction logic for AddCrestronDevice.xaml
    /// </summary>
    public partial class AddCrestronDeviceDialog : Window
    {
        public AddCrestronDeviceDialog(Window owner)
        { 
            this.Owner = owner;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.Title = "Add Known Crestron Device";
            InitializeComponent();
        }

        private void OnAddDeviceClicked(object sender, RoutedEventArgs e)
        {
            if (this.DeviceName.Text != String.Empty && this.DeviceIP.Text != String.Empty)
            {
                this.DialogResult = true;
                this.Close();
            }
        }

        private void OnDeviceNameTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (Utilities.NetworkValidators.IsValidHostname(this.DeviceName.Text)) { this.DeviceName.Background = Brushes.Green; }
            else { this.DeviceName.Background = Brushes.DarkRed; }
        }

        private void OnDeviceIPTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (Utilities.NetworkValidators.IsValidIPAddress(this.DeviceIP.Text)) { this.DeviceIP.Background = Brushes.Green; }
            else { this.DeviceIP.Background = Brushes.DarkRed; }
        }
    }
}
