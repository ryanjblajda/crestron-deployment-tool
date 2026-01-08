using System.Windows;
using System.Windows.Controls;
using CrestronDeploymentTool.Model.Networking;
using CrestronDeploymentTool.Model.Deployment;
using CrestronDeploymentTool.Discovery;
using CrestronDeploymentTool.Model.TargetDevices;
using CrestronDeploymentTool.UserInterface;
using System.Diagnostics;
using System.ComponentModel;
using CrestronDeploymentTool.Utilities;
using System.IO;
using Serilog;
using Serilog.Events;
using Microsoft.Win32;

namespace CrestronDeploymentTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string prefix = "Main |";
        private DeploymentStatusWindow? deploymentStatusWindow;
        public DeploymentStatus Deployment = new DeploymentStatus();
        private CancellationTokenSource? deploymentCancellationToken;

        private string? customUserName = null;
        private string? customPassword = null;
        
        public MainWindow()
        {
            InitializeComponent();
            AvailableNetworkInterfaces.GetAvailableNetworkInterfaces();
            Loaded += OnWindowLoaded;
            this.Deployment.Running = false;
            DiscoveredDevices.AvailableDiscoveredDevices.CollectionChanged += (s, e) => { ClearDeviceList.IsEnabled = DiscoveredDevices.AvailableDiscoveredDevices.Count > 0; };
            ConfigureLogging();
            OpenFileDialog preload = new OpenFileDialog();
            preload.Filter = "All files (*.*) | *.*";
        }

        /// <summary>
        /// configures the logging for the application
        /// </summary>
        private void ConfigureLogging()
        {
            string machineAppDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string machineLoggingPath = Path.Combine(machineAppDataDirectory, "crestron-deployment-tool", "logs");
            Directory.CreateDirectory(machineLoggingPath);
            machineLoggingPath = Path.Combine(machineLoggingPath, "log-");
            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                machineLoggingPath,
                restrictedToMinimumLevel: LogEventLevel.Information,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true,
                outputTemplate: "[{Level:u3} @ {Timestamp:HH:mm:ss}] {Message:lj}\r")
            .WriteTo.Debug(outputTemplate: "[{Level:u3} @ {Timestamp:HH:mm:ss}] {Message:lj}{Newline}{Exception}\r")
            .CreateLogger();
        }

        /// <summary>
        /// a callback when the mainwindow is loaded
        /// </summary>
        /// <param name="sender">the window that fired the callback</param>
        /// <param name="e">event args</param>
        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            Log.Debug($"{prefix} Window Loaded");
            this.GenerateDeploymentWindow();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            try
            {
                if (deploymentStatusWindow != null) { deploymentStatusWindow.Close(); }
            }
            catch(InvalidOperationException ex) { Log.Fatal($"{prefix} InvalidOperationException: {ex}"); }
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
            if (this.Deployment.Running == false)
            {
                //run the main wizard
                (customUserName, customPassword) = DeploymentWizard.ConfigureDeployment(customUserName, customPassword, this);
                //show the deployment window to the user to allow them to begin the deployment or clear all actions if needed
                this.ShowDeploymentWindow();
            }
            else { 
                Log.Debug($"{prefix} Deployment In Progress...Cannot Start Deployment Wizard [Click Ignored]");
                this.ShowDeploymentWindow();
            }
        }

        private void GenerateDeploymentWindow()
        {
            this.deploymentStatusWindow = new DeploymentStatusWindow(this, this.Deployment, this.StartDeployment, this.StopDeploymemt);
            this.deploymentStatusWindow.Closing += (s, e) => { this.deploymentStatusWindow = null; };
        }

        private void ShowDeploymentWindow()
        {
            if (this.deploymentStatusWindow == null) { this.GenerateDeploymentWindow(); }
            if (this.deploymentStatusWindow != null) { this.deploymentStatusWindow.Show(); }
        }

        private async void StartDeployment()
        {
            if (this.Deployment.Running) { Log.Debug($"{prefix} Deployment In Progress... [Click Ignored]"); }
            else
            {
                try
                {
                    this.deploymentCancellationToken = new CancellationTokenSource();
                    this.Deployment.Running = true;

                    Log.Information($"{prefix} Starting Task Thread For Deployment");

                    List<Task> tasks = DiscoveredDevices.SelectedTargetDevices.Select(d =>
                    {
                        return Task.Run(() =>
                        {
                            try
                            {
                                Log.Information($"{prefix} Starting Task Thread For {d.Name} @ {d.IpAddress} Deployment Actions");

                                if (customUserName != null && customPassword != null) { d.Deploy(customUserName, customPassword, deploymentCancellationToken.Token); }
                                else { Log.Error($"{prefix} username and password not provided"); }
                            }
                            catch (OperationCanceledException ex) { Log.Information($"{prefix} Operation Canceled {ex.Message}"); }

                        }, deploymentCancellationToken.Token);
                    }).ToList();

                    await Task.WhenAll(tasks);
                }
                catch (OperationCanceledException ex) { Log.Information($"{prefix} Operation Canceled {ex.Message}"); }
                catch (Exception ex) { Log.Fatal($"{prefix} Exception {ex.Message}"); }
                finally
                {
                    this.Deployment.Running = false;
                    Log.Information($"{prefix} Deployment Tasks Complete");
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

            if (result == true) { DiscoveredDevices.AddDevice(new CrestronDevice(dialog.DeviceName.Text, "", dialog.DeviceIP.Text), DiscoveredDevices.AvailableDiscoveredDevices); }
        }

        private void OnClearDiscoveredDevicesClicked(object sender, RoutedEventArgs e)
        {
            DiscoveredDevices.AvailableDiscoveredDevices.Clear();
        }
    }
}