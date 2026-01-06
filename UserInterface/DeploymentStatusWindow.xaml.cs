using CrestronDeploymentTool.Model.Deployment;
using CrestronDeploymentTool.Model.TargetDevices;
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
    /// Interaction logic for DeploymentStatus.xaml
    /// </summary>
    public partial class DeploymentStatusWindow : Window
    {
        private Action start;
        private Action stop;

        public DeploymentStatusWindow(Window owner, DeploymentStatus status, Action start, Action stop)
        {
            InitializeComponent();
            this.start = start;
            this.stop = stop;
            this.DataContext = status;
            this.Owner = owner;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        private void OnClearDeploymentActionsClicked(object sender, RoutedEventArgs e)
        {
            lock (DiscoveredDevices.SelectedTargetDevices)
            {
                DiscoveredDevices.SelectedTargetDevices.ToList().ForEach(d => { d.DeploymentActions.Clear(); });
            }
        }

        private void OnBeginDeploymentClicked(object sender, RoutedEventArgs e)
        {
            this.start();
        }

        private void OnCancelDeploymentClicked(object sender, RoutedEventArgs e)
        {
            this.stop();   
        }
    }
}
