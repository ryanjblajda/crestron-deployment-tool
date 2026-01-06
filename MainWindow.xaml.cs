using System.Windows;
using System.Windows.Controls;
using CrestronDeploymentTool.Model.Networking;
using CrestronDeploymentTool.Model.Deployment;
using CrestronDeploymentTool.Discovery;
using CrestronDeploymentTool.Model.TargetDevices;
using CrestronDeploymentTool.UserInterface;
using System.Diagnostics;
using System.ComponentModel;

namespace CrestronDeploymentTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string prefix = "Main |";
        private DeploymentStatusWindow? deploymentStatusWindow;
        private DeploymentStatus deploymentStatus = new DeploymentStatus();
        private CancellationTokenSource? deploymentCancellationToken;

        private string? customUserName;
        private string? customPassword;

        public MainWindow()
        {
            InitializeComponent();
            AvailableNetworkInterfaces.GetAvailableNetworkInterfaces();
            Loaded += OnWindowLoaded;
            this.deploymentStatus.Running = false;
            DiscoveredDevices.AvailableDiscoveredDevices.CollectionChanged += (s, e) => { ClearDeviceList.IsEnabled = DiscoveredDevices.AvailableDiscoveredDevices.Count > 0; };
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            this.GenerateDeploymentWindow();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (deploymentStatusWindow != null) { deploymentStatusWindow.Close(); }
        }

        private void OnDeploymentItemSelectionChanged(object sender, SelectionChangedEventArgs e)
        {            
            if (DeploymentWizardActions.Options.Any(i => i.IsSelected) && DiscoveredDevices.AvailableDiscoveredDevices.Any(d => d.IsSelected)) { DeploymentButton.IsEnabled = true; }
            else { DeploymentButton.IsEnabled = false; }
        }

        private void OnNetworkInterfaceSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AvailableNetworkInterfaces.Interfaces.Any(i => i.IsSelected)) { DiscoveryButton.IsEnabled = true; }
            else { DiscoveryButton.IsEnabled = false; }
        }

        private void OnDiscoveredDeviceSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DeploymentWizardActions.Options.Any(i => i.IsSelected) && DiscoveredDevices.AvailableDiscoveredDevices.Any(d => d.IsSelected)) { DeploymentButton.IsEnabled = true; }
            else { DeploymentButton.IsEnabled = false; }
        }

        private void OnDiscoveryClicked(object sender, RoutedEventArgs e)
        {
            Task.Run(() => { int discovered = CrestronDeviceDiscovery.DiscoverDevices(AvailableNetworkInterfaces.Interfaces.Where(i => i.IsSelected == true).ToList()); });
        }

        private void OnDeploymentWizardClicked(object sender, RoutedEventArgs e)
        {
            if (this.deploymentStatus.Running == false)
            {
                List<DeploymentWizardAction> selectedActions = DeploymentWizardActions.Options.Where(i => i.IsSelected).ToList();
                bool cancel = false;

                //if both of these are null, this is the first run
                if (customPassword == null && customPassword == null) { (customUserName, customPassword, cancel) = DeploymentWizard.ConnectionDetails(this); }
                //if this is not the first run, provide a quick confirmation to re-use the existing credentials
                else 
                {
                    MessageBoxResult result = MessageBox.Show("Do you want to use the credentials you provided previously?", "Use Existing Credentials", MessageBoxButton.YesNo);
                    if (result == MessageBoxResult.No) { (customUserName, customPassword, cancel) = DeploymentWizard.ConnectionDetails(this); }
                }

                //if the user cancels the operation, exit the wizard
                if (cancel) { return; }

                //determine if we should provide a re-selection dialog for each of the deployment actions (users may want to only deploy to certain devices
                bool reselectTargetDevices = selectedActions.Count > 1;

                //run through the wizard for each deployment action selected
                selectedActions.ForEach(action =>
                {
                    bool canceled = false;

                    switch (action.Name)
                    {
                        case (DeploymentWizardActions.SendConsoleCommands):
                            canceled = DeploymentWizard.ConsoleCommands(reselectTargetDevices);
                            Debug.WriteLine($"{prefix} Console Command Deployment {(canceled == true ? "Canceled" : "Ready")}");
                            break;
                        case (DeploymentWizardActions.SendConfigurationFiles):
                            canceled = DeploymentWizard.ConfigurationFiles(reselectTargetDevices);
                            Debug.WriteLine($"{prefix} Configuration File Deployment {(canceled == true ? "Canceled" : "Ready")}");
                            break;
                        case (DeploymentWizardActions.SendUserInterfaces):
                            canceled = DeploymentWizard.UserInterfaces(reselectTargetDevices);
                            Debug.WriteLine($"{prefix} User Interface Deployment {(canceled == true ? "Canceled" : "Ready")}");
                            break;
                        case (DeploymentWizardActions.SendProgramming):
                            canceled = DeploymentWizard.Programming(reselectTargetDevices);
                            Debug.WriteLine($"{prefix} Programming Deployment {(canceled == true ? "Canceled" : "Ready")}");
                            break;
                        case (DeploymentWizardActions.SendFirmware):
                            canceled = DeploymentWizard.Firmware(reselectTargetDevices);
                            Debug.WriteLine($"{prefix} Firmware Deployment {(canceled == true ? "Canceled" : "Ready")}");
                            break;
                    }
                });

                //show the deployment window to the user to allow them to begin the deployment or clear all actions if needed
                this.ShowDeploymentWindow();
            }
            else { 
                Debug.WriteLine($"{prefix} Deployment In Progress...Cannot Start Deployment Wizard [Click Ignored]");
                this.ShowDeploymentWindow();
            }
        }

        private void GenerateDeploymentWindow()
        {
            this.deploymentStatusWindow = new DeploymentStatusWindow(this, this.deploymentStatus, this.StartDeployment, this.StopDeploymemt);
            this.deploymentStatusWindow.Closing += (s, e) => { this.deploymentStatusWindow = null; };
        }

        private void ShowDeploymentWindow()
        {
            if (this.deploymentStatusWindow == null) { this.GenerateDeploymentWindow(); }
            if (this.deploymentStatusWindow != null) { this.deploymentStatusWindow.Show(); }
        }

        private async void StartDeployment()
        {
            if (this.deploymentStatus.Running) { Debug.WriteLine($"{prefix} Deployment In Progress... [Click Ignored]"); }
            else
            {
                try
                {
                    this.deploymentCancellationToken = new CancellationTokenSource();
                    this.deploymentStatus.Running = true;

                    Debug.WriteLine($"{prefix} Starting Task Thread For Deployment");

                    List<Task> tasks = DiscoveredDevices.SelectedTargetDevices.Select(d =>
                    {
                        return Task.Run(() =>
                        {
                            try
                            {
                                Debug.WriteLine($"{prefix} Starting Task Thread For {d.Name} @ {d.IpAddress} Deployment Actions");

                                if (customUserName != null && customPassword != null) { d.Deploy(customUserName, customPassword, deploymentCancellationToken.Token); }
                                else { Debug.WriteLine($"{prefix} username and password not provided"); }
                            }
                            catch (OperationCanceledException ex) { Debug.WriteLine($"{prefix} Operation Canceled {ex.Message}"); }

                        }, deploymentCancellationToken.Token);
                    }).ToList();

                    await Task.WhenAll(tasks);
                }
                catch (OperationCanceledException ex) { Debug.WriteLine($"{prefix} Operation Canceled {ex.Message}"); }
                catch (Exception ex) { Debug.WriteLine($"{prefix} Exception {ex.Message}"); }
                finally
                {
                    this.deploymentStatus.Running = false;
                    Debug.WriteLine($"{prefix} Deployment Tasks Complete");
                }
            }
        }

        private void StopDeploymemt()
        {
            if (deploymentCancellationToken != null) deploymentCancellationToken.Cancel();
        }

        private void OnAddManualDeviceClicked(object sender, RoutedEventArgs e)
        {
            AddCrestronDeviceDialog dialog = new AddCrestronDeviceDialog(this);
            bool? result = dialog.ShowDialog();

            if (result == true) { DiscoveredDevices.AddDevice(new CrestronDevice(dialog.DeviceName.Text, "", dialog.DeviceIP.Text)); }
        }

        private void OnClearDiscoveredDevicesClicked(object sender, RoutedEventArgs e)
        {
            DiscoveredDevices.AvailableDiscoveredDevices.Clear();
        }
    }
}