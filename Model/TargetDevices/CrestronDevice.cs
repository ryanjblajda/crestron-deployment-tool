using CrestronDeploymentTool.Model.TargetDevices.DeviceDeployment;
using CrestronDeploymentTool.Utilities;
using Renci.SshNet;
using Renci.SshNet.Common;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace CrestronDeploymentTool.Model.TargetDevices
{
    /// <summary>
    /// a class to represent a target crestron device
    /// </summary>
    public class CrestronDevice : INotifyPropertyChanged
    {
        private const string prefix = "Target Device |";

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); } }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string IpAddress { get; private set; }
        public string Model { get; private set; }

        internal SshClient? SshClient { get; private set; }
        internal TcpClient? TcpClient { get; private set; }
        internal SftpClient? SftpClient { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<DeviceDeploymentAction> DeploymentActions { get; private set; }

        protected void OnPropertyChanged(string property)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }

        public CrestronDevice(string name, string description, string ipaddress)
        {
            DeploymentActions = new ObservableCollection<DeviceDeploymentAction>();

            Debug.WriteLine($"{prefix} Creating New Device: {name} => {description} @ {ipaddress}");

            Name = name.ToUpper();
            Description = description.ToUpper();
            Model = description.Split("\x20")[0].Trim().ToUpper();
            IpAddress = ipaddress;
        }

        /// <summary>
        /// begins the deployment of all actions assigned to the device
        /// </summary>
        /// <param name="username">ssh username</param>
        /// <param name="password">ssh password</param>
        /// <param name="token">cancellation token to stop the deployment if requested</param>
        public void Deploy(string username, string password, CancellationToken token)
        {
            if (Cancellation.CheckTokenStatus(token)) return;

            List<DeviceDeploymentAction> deploy = new List<DeviceDeploymentAction>();

            Debug.WriteLine($"{prefix} Device: {this.Name} @ {this.IpAddress} | Beginning Deployment");

            lock (this.DeploymentActions) { deploy = this.DeploymentActions.ToList(); }

            Debug.WriteLine($"{prefix} Device: {this.Name} @ {this.IpAddress} | Creating SSH Client => Credentials {username} {password}");
            
            this.SshClient = new SshClient(this.IpAddress, username, password);
            this.SftpClient = new SftpClient(this.IpAddress, username, password);
            this.TcpClient = new TcpClient();

            deploy.ForEach(action => 
            {
                Debug.WriteLine($"{prefix} Device: {this.Name} @ {this.IpAddress} | Beginning Task: {action.Description}");
                
                bool? result = action.Invoke(token);
                
                if (result != null) {
                    string message = $"Task: {action.Description} => {(result == true ? "Completed Successfully" : "Failed To Complete")}";
                    action.Status = result == true ? DeviceDeploymentActionStatus.CompleteSuccess : DeviceDeploymentActionStatus.CompleteFailure;
                }
            });

            Debug.WriteLine($"{prefix} Device: {this.Name} @ {this.IpAddress} | Deployment Complete");
        }

        /// <summary>
        /// connect to the device's console to send commands as needed
        /// </summary>
        /// <param name="action">a reference to the device deployment action who called this function, allowing us to update it's status</param>
        /// <returns>the valid method for console command sending</returns>
        internal ConnectionResult ConnectConsole(DeviceDeploymentAction action)
        {
            ConnectionResult result = ConnectionResult.ConnectionFailure;

            if (this.SshClient != null)
            {
                action.Status = DeviceDeploymentActionStatus.Waiting;
                action.Message = "Attempting to connect with SSH";
                Debug.WriteLine($"{prefix} Device: {this.Name} @ {this.IpAddress} | Attempting to connect with SSH");

                try
                {
                    if (!this.SshClient.IsConnected) { this.SshClient.Connect(); }
                    action.Status = DeviceDeploymentActionStatus.SSHSuccess;
                    Debug.WriteLine($"{prefix} Device: {this.Name} @ {this.IpAddress} | Connected via SSH");

                    result = ConnectionResult.UseSsh;
                }
                catch (Exception sshEx)
                {
                    action.Status = DeviceDeploymentActionStatus.SSHFailed;
                    action.Message = sshEx.Message;

                    if (sshEx.GetType() != typeof(SshAuthenticationException))
                    {
                        try
                        {
                            if (this.TcpClient?.Connected == false) { this.TcpClient?.Connect(new IPEndPoint(IPAddress.Parse(this.IpAddress), 23)); }

                            Debug.WriteLine($"{prefix} Device: {this.Name} @ {this.IpAddress} | Attempting to connect with Telnet");
                            
                            action.Status = DeviceDeploymentActionStatus.TelnetSuccess;
                            result = ConnectionResult.UseTelnet;

                            Debug.WriteLine($"{prefix} Device: {this.Name} @ {this.IpAddress} | Connected via Telnet");
                        }
                        catch (Exception telnetEx)
                        {
                            action.Status = DeviceDeploymentActionStatus.TelnetFailed;
                            action.Message = telnetEx.Message;
                        }
                    }
                }
            }
            else
            {
                action.Status = DeviceDeploymentActionStatus.CompleteFailure;
                action.Message = $"SSH Client for Device is null!";
            }

            return result;
        }

        /// <summary>
        /// connect to the device's sftp server to upload files
        /// </summary>
        /// <param name="action">a reference to the device deployment action who called this function, allowing us to update it's status</param>
        /// <returns>the valid method for console command sending</returns>
        internal ConnectionResult ConnectSFTP(DeviceDeploymentAction action)
        {
            ConnectionResult result = ConnectionResult.ConnectionFailure;
            action.Message = "Attempting to connect SFTP";
            Debug.WriteLine($"{prefix} Device: {this.Name} @ {this.IpAddress} | Attempting to connect with SFTP");

            if (this.SftpClient != null)
            {
                try {
                    if (!this.SftpClient.IsConnected) { this.SftpClient.Connect(); } 
                }
                catch (Exception ex)
                {
                    action.Status = DeviceDeploymentActionStatus.SendingFileFailed;
                    action.Message = $"Unable to connect via SFTP: {ex.Message}";
                }

                result = ConnectionResult.UseSsh;
                Debug.WriteLine($"{prefix} Device: {this.Name} @ {this.IpAddress} | Connected to SFTP");
            }
            else { 
                action.Status = DeviceDeploymentActionStatus.CompleteFailure;
                action.Message = "SFTP Client NULL!";
            }

            return result;
        }
    }
}
