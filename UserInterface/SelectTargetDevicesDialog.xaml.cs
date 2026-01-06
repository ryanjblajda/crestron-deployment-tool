using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    /// Interaction logic for SelectTargetDevicesDialog.xaml
    /// </summary>
    public partial class SelectTargetDevicesDialog : Window
    {
        public SelectTargetDevicesDialog(Window? owner, string action, List<string> devices)
        {
            if (owner != null)
            {
                this.Owner = owner;
                this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

                InitializeComponent();
                this.Title = $"Target Devices for {action}";
                this.TargetDevices.ItemsSource = devices;
                this.Prompt.Text = $"You have selected multiple deployment actions, please specify what devices you would like to {action.ToUpper()} to. You can also close this window to deploy to all previously selected devices";
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            //make sure we return false when close is touched
            if (this.DialogResult == null) { this.DialogResult = false; }
            base.OnClosing(e);
        }

        private void OnConfirmSelectionClicked(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
