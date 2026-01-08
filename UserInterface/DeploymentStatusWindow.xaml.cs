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
        private DeploymentStatus deploy;

        public DeploymentStatusWindow(MainWindow owner, DeploymentStatus status, Action start, Action stop)
        {
            InitializeComponent();
            this.start = start;
            this.stop = stop;
            this.DataContext = status;
            this.Owner = owner;
            this.deploy = owner.Deployment;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.Closing += OnClosing;
        }

        /// <summary>
        /// asks the user to make sure to remember to clear out the deployment list if required
        /// </summary>
        /// <param name="sender">the window that fired the event</param>
        /// <param name="e">the event args</param>
        private void OnClosing(object? sender, CancelEventArgs e)
        {
            //dont allow removal of deployment items unless deployment is idle
            if (this.deploy.Idle)
            {
                //only show if any of the selected target devices have deployment actions added
                if (DiscoveredDevices.SelectedTargetDevices.Any(d => d.DeploymentActions.Count > 0))
                {
                    MessageBoxResult result = ConfirmationDialog.Show("Do you want to *clear* the deployment action list?", "Clear Deployment", MessageBoxButton.YesNo);
                    //old version using default messagebox
                    //MessageBoxResult result = MessageBox.Show("Do you want to clear the deployment action list?", "Clear Deployment", MessageBoxButton.YesNo);
                    if (result == MessageBoxResult.Yes) { ClearDeploymentActions(); }
                }
            }
        }

        /// <summary>
        /// clears all deployment actions on the selected target devices
        /// </summary>
        private void ClearDeploymentActions()
        {
            lock (DiscoveredDevices.SelectedTargetDevices)
            {
                DiscoveredDevices.SelectedTargetDevices.ToList().ForEach(d => { d.DeploymentActions.Clear(); });
            }
        }

        /// <summary>
        /// event handler when clear deployment actions is clicked
        /// </summary>
        /// <param name="sender">the button that sent the event</param>
        /// <param name="e">routed event args</param>
        private void OnClearDeploymentActionsClicked(object sender, RoutedEventArgs e)
        {
            ClearDeploymentActions();
        }

        /// <summary>
        /// event handler to start deployment
        /// </summary>
        /// <param name="sender">the button that sent the event</param>
        /// <param name="e">routed event args</param>
        private void OnBeginDeploymentClicked(object sender, RoutedEventArgs e)
        {
            this.start();
        }

        /// <summary>
        /// event handler to stop deployment
        /// </summary>
        /// <param name="sender">the button that sent the event</param>
        /// <param name="e">routed event args</param>
        private void OnCancelDeploymentClicked(object sender, RoutedEventArgs e)
        {
            this.stop();   
        }
    }
}
