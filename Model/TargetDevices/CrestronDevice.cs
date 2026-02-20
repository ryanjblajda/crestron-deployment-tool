using CrestronDeploymentTool.Model.Deployment;
using CrestronDeploymentTool.Model.Deployment.ProvisioningState;
using CrestronDeploymentTool.Model.TargetDevices.DeviceDeployment;
using CrestronDeploymentTool.Utilities;
using Renci.SshNet;
using Renci.SshNet.Common;
using Serilog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Text;
using System.Security.Cryptography;

namespace CrestronDeploymentTool.Model.TargetDevices
{
    /// <summary>
    /// a class to represent a target crestron device
    /// </summary>
    public class CrestronDevice : INotifyPropertyChanged
    {
        private const string prefix = "Crestron Device |";

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); } }
        public string FirmwareVersion { get; internal set; }
        public string Model { get; internal set; }
        public string Serial { get; private set; }

        private string NetworkConfigurationPattern = @"ip address[\s\.]*:[\s]*(?<ipaddress>[\d]+\.[\d]+\.[\d]+\.[\d]+)[\s\S]*subnet mask[\s\.]*:[\s]*(?<subnet>[\d]+.[\d]+.[\d]+.[\d]+)[\s\S]*(default\s*gateway|def\s*router)[\s\.]*:[\s]*((?<gateway>[\d]+\.[\d]+\.[\d]+\.[\d]+))?";

        public DeviceNetworkConfiguration NetworkConfiguration { get; private set; }

        internal SshClient? SshClient;
        internal TcpClient? TcpClient;
        internal SftpClient? SftpClient;

        public event PropertyChangedEventHandler? PropertyChanged;
        public ObservableCollection<DeviceDeploymentAction> DeploymentActions { get; private set; }

        protected void OnPropertyChanged(string property)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }

        public CrestronDevice(string name, string model, string serial, string firmware, string ipaddress)
        {
            DeploymentActions = new ObservableCollection<DeviceDeploymentAction>();

            this.NetworkConfiguration = new DeviceNetworkConfiguration(name.ToUpper(), ipaddress);
            
            FirmwareVersion = firmware;
            Model = model.ToUpper();
            Serial = serial;

            Log.Information($"{prefix} Creating New Device: {name} => {model} @ {ipaddress} [{serial}]");
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

            Log.Information($"{prefix} Device: {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} | Beginning Deployment");

            lock (this.DeploymentActions) { deploy = this.DeploymentActions.ToList(); }

            this.SftpClient = new SftpClient(this.NetworkConfiguration.IPAddress, username, password);
            this.TcpClient = new TcpClient();

            deploy.ForEach(action => 
            {
                Log.Information($"{prefix} Device: {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} | Beginning Task: {action.Description}");

                if (action.Name == DeploymentWizardActions.ProvisionNewDevice) {
                    this.SshClient = new SshClient(this.NetworkConfiguration.IPAddress, "crestron", "");
                    Log.Information($"{prefix} Device: {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} | Creating SSH Client => Credentials: {Constants.CrestronDefaultUsername} // **{Constants.CrestronDefaultPassword}**");
                }
                else {
                    //make sure that we use the provided credentials if a previous operation required the default credentials for provisioing a new device
                    if (this.SshClient == null || this.SshClient?.ConnectionInfo.Username == "crestron")
                    {
                        this.SshClient = new SshClient(this.NetworkConfiguration.IPAddress, username, password);
                        Log.Information($"{prefix} Device: {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} | Creating SSH Client => Credentials: {username} // {password}");
                    }
                }

                bool? result = action.Invoke(token);
                
                if (result != null) {
                    Log.Information($"{prefix} Device: {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} | Task: {action.Description} => {(result == true ? "Completed Successfully" : "Failed To Complete")}");
                    string message = $"Task: {action.Description} => {(result == true ? "Completed Successfully" : "Failed To Complete")}";
                    action.Status = result == true ? DeviceDeploymentActionStatus.CompleteSuccess : DeviceDeploymentActionStatus.CompleteFailure;
                }
            });

            Log.Information($"{prefix} Device: {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} | Disconnecting All Clients");

            if (this.SftpClient.IsConnected) { this.SftpClient?.Disconnect(); }
            if (this.SshClient?.IsConnected == true) { this.SshClient?.Disconnect(); }
            if (this.TcpClient?.Connected == true) { this.TcpClient?.Close(); }

            Log.Information($"{prefix} Device: {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} | Deployment Complete");
        }

        /// <summary>
        /// connect to the device's console to send commands as needed
        /// </summary>
        /// <param name="action">a reference to the device deployment action who called this function, allowing us to update it's status</param>
        /// <returns>the valid method for console command sending</returns>
        internal ConnectionResult ConnectConsole(DeviceDeploymentAction action)
        {
            ConnectionResult result = ConnectionResult.ConnectionFailure;

            if (this.SshClient == null) { this.SshClient = new SshClient(this.NetworkConfiguration.IPAddress, DeploymentResources.customUserName, DeploymentResources.customPassword); }

            if (this.SshClient != null)
            {
                action.Status = DeviceDeploymentActionStatus.Waiting;
                action.Message = "Attempting to connect with SSH";
                Log.Information($"{prefix} Device: {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} | Attempting to connect with SSH");

                try
                {
                    if (!this.SshClient.IsConnected) { this.SshClient.Connect(); }

                    action.Status = DeviceDeploymentActionStatus.SSHSuccess;
                    Log.Information($"{prefix} Device: {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} | Connected via SSH");

                    result = ConnectionResult.UseSsh;
                }
                catch (Exception sshEx)
                {
                    action.Status = DeviceDeploymentActionStatus.SSHFailed;
                    action.Message = sshEx.Message;
                    Log.Information($"{prefix} Device: {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} | Failure to Connect via SSH");

                    if (sshEx.GetType() != typeof(SshAuthenticationException))
                    {
 
                        try
                        {
                            if (this.TcpClient == null) {
                                Log.Information($"{prefix} Device: {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} | Creating New TCP Client");
                                this.TcpClient = new TcpClient(); 
                            }                            
                            
                            if (this.TcpClient?.Connected == false) {
                                Log.Information($"{prefix} Device: {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} | Attempting to connect with Telnet");
                                this.TcpClient?.Connect(new IPEndPoint(IPAddress.Parse(this.NetworkConfiguration.IPAddress), Constants.TelnetPort)); 
                            }

                            if (this.TcpClient?.Connected == true)
                            {
                                action.Status = DeviceDeploymentActionStatus.TelnetSuccess;
                                action.Message = "Connected via Telnet";
                                result = ConnectionResult.UseTelnet;

                                Log.Information($"{prefix} Device: {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} | Connected via Telnet");
                            }
                            
                        }
                        catch (Exception telnetEx)
                        {
                            action.Status = DeviceDeploymentActionStatus.TelnetFailed;
                            action.Message = telnetEx.Message;
                            Log.Error($"{prefix} Device: {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} -> {telnetEx.Message}");
                        }
                    }
                    else if (sshEx.GetType() == typeof(SshAuthenticationException))
                    {
                        Log.Error($"{prefix} Device: {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} | Credentials Incorrect!");
                        action.Status = DeviceDeploymentActionStatus.SSHFailed;
                        action.Message = "SSH Credentials Incorrect!";
                    }
                }
            }
            else
            {
                action.Status = DeviceDeploymentActionStatus.CompleteFailure;
                action.Message = $"SSH Client for Device is null!";
                Log.Fatal($"{prefix} Device: {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} | SSH Client Null!");
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
            Log.Information($"{prefix} Device: {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} | Attempting to connect with SFTP");

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
                Log.Information($"{prefix} Device: {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} | Connected to SFTP");
            }
            else { 
                action.Status = DeviceDeploymentActionStatus.CompleteFailure;
                action.Message = "SFTP Client NULL!";
            }

            return result;
        }

        /// <summary>
        /// updates the console response field with data incoming from the device
        /// </summary>
        /// <param name="line">the newest line of data</param>
        /// <param name="action">the action that called this</param>
        private void UpdateResponse(string? line, DeviceDeploymentAction action)
        {
            if (line != null)
            {
                //replace the bs that the terminal gives us when its a new line
                line = line.Replace($"{this.Model}>", String.Empty);
                line = line.Replace($"{this.Model.ToLower()}>", String.Empty);
                //if the line is now an empty string, ignore it
                if (line != String.Empty)
                {
                    //append until we hit 255 characters
                    if (action.Response.Length < 1024) { action.Response += $"\r" + line; }
                    else { action.Response = line; }
                    //Log.Debug($"{prefix} Command Response Line: {line}");
                }
            }
        }

        /// <summary>
        /// sends a command to a device via Telnet and waits for a response
        /// </summary>
        /// <param name="command">the command to send</param>
        /// <param name="action">a reference to the action that called the function</param>
        /// <returns></returns>
        private (bool, string) SendCommandTelnet(string command, DeviceDeploymentAction action)
        {
            bool success = false;
            string buffer = "";
            bool disconnect = false;

            Timer disconnectTimer = new Timer((object? obj) => { Log.Debug($"{prefix} Disconnect");  disconnect = true; });

            try
            {
                if (this.TcpClient != null)
                {
                    this.TcpClient.ReceiveTimeout = 100;

                    if (this.TcpClient?.Connected == false) { this.ConnectConsole(action); }

                    Log.Debug($"{prefix} Running Command: {command} on {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} via Telnet");

                    this.TcpClient?.GetStream().Write(Encoding.ASCII.GetBytes($"{command}\r"));

                    action.Message = "Command Sent";
                    action.Status = DeviceDeploymentActionStatus.WaitingForResponse;

                    if (this.TcpClient?.GetStream().CanRead == true)
                    {
                        //Log.Debug($"{!disconnect}");
                        
                        while (!disconnect)
                        {
                            byte[] buf = new byte[1024];

                            if (this.TcpClient != null)
                            {
                                int? read = this.TcpClient?.GetStream()?.Read(buf, 0, this.TcpClient.Available);
                                
                                Log.Debug($"{prefix} Read {read} Bytes");

                                if (read != null)
                                {
                                    string incoming = Encoding.ASCII.GetString(buf, 0, (int)read);

                                    if (read > 0)
                                    {
                                        disconnectTimer.Change(1000, Timeout.Infinite);
                                        
                                        buffer += incoming;
                                        Log.Debug($"{prefix} Incoming Telnet Data -> {Utilities.TextHelpers.CleanString(incoming)}");
                                        Log.Debug($"{prefix} Reset Disconnect Timer");
                                        UpdateResponse(incoming, action);
                                    }
                                }
                            }
                        }

                        success = true;
                        action.Status = DeviceDeploymentActionStatus.SendingCommandSuccess;
                    }
                }
                else
                {
                    Log.Fatal($"{prefix} TCP Client is Null!");
                }
            }
            catch (Exception ex) {
                Log.Fatal($"{prefix} {ex.Message}");
                action.Message = ex.Message; 
            }

            return (success, buffer);
        }

        /// <summary>
        /// sends a command to a device via SSH and waits for a response
        /// </summary>
        /// <param name="command">the command to send</param>
        /// <param name="action">a reference to the action that called the function</param>
        /// <param name="cancel">a cancellation token to listen to so that this action can be cancelled if needed</param>
        /// <returns></returns>
        private (bool, string?) SendCommandSsh(string command, DeviceDeploymentAction action, CancellationToken cancel)
        {
            bool success = false;
            string? result = null;

            if (this.SshClient == null) {

                this.SshClient = new SshClient(this.NetworkConfiguration.IPAddress, DeploymentResources.customUserName, DeploymentResources.customPassword);
            }

            if (this.SshClient != null)
            {
                try
                {
                    if (!this.SshClient.IsConnected) { this.SshClient.Connect(); }

                    action.Status = DeviceDeploymentActionStatus.SendingCommand;

                    if (command != String.Empty)
                    {
                        action.Status = DeviceDeploymentActionStatus.Waiting;
                        Log.Debug($"{prefix} Running Command: {command} on {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} via SSH");

                        if (this.SshClient?.IsConnected == false) { this.ConnectConsole(action); }
                        
                        SshCommand? cmd = this.SshClient?.RunCommand(command + "\r");
                        action.Message = "Command Sent";

                        if (cmd != null)
                        {
                            action.Status = DeviceDeploymentActionStatus.WaitingForResponse;

                            Log.Debug($"{prefix} {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} SSH Command Result: {Utilities.TextHelpers.CleanString(cmd?.Result)} {(cmd?.Error == String.Empty ? "" : " // Error:" + cmd?.Error)} {(cmd?.ExitStatus == null ? "" : " // Exit Status:" + cmd?.ExitStatus)}");

                            result = cmd?.Result;

                            if (cmd?.Result != null) { UpdateResponse(cmd.Result, action); }

                            if (cmd?.Error != null) { UpdateResponse(cmd?.Error, action); }

                            if (cmd?.ExitStatus == 0 || cmd?.Error == String.Empty)
                            {
                                Log.Information($"{prefix} Command {command} Successfully sent to {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress}");
                                action.Status = DeviceDeploymentActionStatus.SendingCommandSuccess;
                                success = true;
                            }
                            else
                            {
                                Log.Information($"{prefix} Command {command} Failure sending to {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress}");
                                action.Status = DeviceDeploymentActionStatus.SendingCommandFailed;
                            }
                        }
                        else { Log.Warning($"{prefix} Device: {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} Command is null!"); }
                    }
                    else { Log.Debug($"{prefix} Device: {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} Command is empty string!!"); }
                }
                catch (Exception ex)
                {
                    success = false;
                    action.Status = DeviceDeploymentActionStatus.SendingCommandFailed;
                    action.Message += $"\r{ex.Message}";
                    Log.Fatal($"{prefix} Device: {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} -> SSH: {ex.Message}");
                }
            }
            else
            {
                action.Status = DeviceDeploymentActionStatus.CompleteFailure;
                action.Message = "SSH Client Null!";
                Log.Warning($"{prefix} Device: {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} -> SSH: {action.Message}");
            }

            return (success, result);
        }

        /// <summary>
        /// sends a console command to a device
        /// </summary>
        /// <param name="command">the command to send</param>
        /// <param name="action">a reference to the action that called the function</param>
        /// <param name="cancel">a cancellation token to listen to so that this action can be cancelled if needed</param>
        /// <returns>a bool representing whether or not the command was sent to the device, not necessarily whether the command has a positive response</returns>
        public (bool, string) SendConsoleCommand(string command, DeviceDeploymentAction? action, CancellationToken cancel)
        {
            if (Cancellation.CheckTokenStatus(cancel, action)) return (false, "");

            bool success = false;
            string? incoming = String.Empty;

            //make sure that there is a deployment action regardless of what is passed.
            action = action == null ? new DeviceDeploymentAction("", "") : action;

            ConnectionResult connection = this.ConnectConsole(action);

            switch (connection)
            {
                case ConnectionResult.UseSsh:
                    (success, incoming) = SendCommandSsh(command, action, cancel);
                    break;
                case ConnectionResult.UseTelnet:
                    (success, incoming) = SendCommandTelnet(command, action);
                    break;
            }

            if (incoming == null) { incoming = String.Empty; }

            return (success, incoming);
        }

        public string? SendConsoleCommandWithResponse(string command, CancellationToken cancel)
        {
            string response = SendConsoleCommand(command, new DeviceDeploymentAction("dummy", ""), cancel).Item2;

            return response;
        }

        /// <summary>
        /// sends a file via memory stream to a device [zig file]
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="remotefilepath"></param>
        /// <param name="postUploadCommand"></param>
        /// <param name="action">a reference to the action that called the function</param>
        /// <param name="cancel">a cancellation token to listen to so that this action can be cancelled if needed</param>
        /// <returns>a bool representing if the file was sent to the device, and the command sent (if provided) </returns>
        public bool SendFileViaFTP(MemoryStream? stream, string remotefilepath, string postUploadCommand, DeviceDeploymentAction action, CancellationToken cancel)
        {
            if (Cancellation.CheckTokenStatus(cancel, action)) return false;

            bool success = false;
            ConnectionResult result = ConnectionResult.ConnectionFailure;

            result = this.ConnectSFTP(action);

            if (result == ConnectionResult.UseSsh)
            {
                this.ConnectConsole(action);

                Log.Information($"{prefix} Attempting to Upload MemoryStream to {remotefilepath} on {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress}");

                try
                {
                    //reset stream
                    stream?.Seek(0, SeekOrigin.Begin);
                    //try to upload
                    if (stream != null)
                    {
                        this.SftpClient?.UploadFile(stream, remotefilepath, uploadProgress =>
                        {
                            if (Cancellation.CheckTokenStatus(cancel, action)) this.SftpClient.Disconnect();
                            else
                            {
                                action.Status = DeviceDeploymentActionStatus.SendingFile;
                                double percent = uploadProgress * 100 / (ulong)stream.Length;
                                action.Message = $"Upload Progress: {percent}% [{uploadProgress} bytes]";
                                //Log.Debug($"{prefix} {action.Message}");
                            }
                        });

                        if (Cancellation.CheckTokenStatus(cancel, action)) { return false; }
                        ;

                        success = true;
                        action.Status = DeviceDeploymentActionStatus.SendingFileSuccess;
                        Log.Information($"{prefix} Sent File {remotefilepath} via SFTP");

                        SendConsoleCommand(postUploadCommand, action, cancel);
                    }
                    else
                    {
                        Log.Error($"{prefix} MemoryStream for {remotefilepath} is null!! Unable to upload to {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    action.Status = DeviceDeploymentActionStatus.SendingFileFailed;
                    action.Message = $"Failed to upload file via SFTP: {ex.Message}";
                    Log.Fatal($"{prefix} Failed to upload file via SFTP: {ex.Message}");
                }
            }

            return success;
        }

        /// <summary>
        /// sends a file from a local file path to a device
        /// </summary>
        /// <param name="localfilepath">the full file path to the file</param>
        /// <param name="remotefilepath">the path on the device the file should be uploaded to</param>
        /// <param name="postUploadCommand">a post upload command to be run</param>
        /// <param name="action">a reference to the action that called the function</param>
        /// <param name="cancel">a cancellation token to listen to so that this action can be cancelled if needed</param>
        /// <returns>a bool representing if the file was sent to the device, and the command sent (if provided) </returns>
        public bool SendFileViaFTP(string localfilepath, string remotefilepath, string postUploadCommand, DeviceDeploymentAction action, CancellationToken cancel)
        {
            if (Cancellation.CheckTokenStatus(cancel, action)) return false;

            bool success = false;
            ConnectionResult result = ConnectionResult.ConnectionFailure;

            result = this.ConnectSFTP(action);

            if (result == ConnectionResult.UseSsh)
            {
                this.ConnectConsole(action);

                try
                {
                    Task.Run(() =>
                    {
                        Log.Debug($"{prefix} Opening Local File {localfilepath}");
                        FileStream fileStream = File.OpenRead(localfilepath);
                        Log.Information($"{prefix} Attempting to Upload {localfilepath} to {remotefilepath} on {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress}");

                        try
                        {
                            this.SftpClient?.UploadFile(fileStream, remotefilepath, uploadProgress =>
                            {
                                if (this != null)
                                {
                                    if (Cancellation.CheckTokenStatus(cancel, action))
                                    {
                                        Log.Warning($"{prefix} Upload Canceled");
                                        
                                        if (this.SftpClient != null) { this.SftpClient?.Disconnect(); }
                                    }
                                    else
                                    {
                                        try
                                        {
                                            action.Status = DeviceDeploymentActionStatus.SendingFile;
                                            double percent = uploadProgress * 100 / (ulong)fileStream.Length;
                                            action.Message = $"Upload Progress: {percent}% [{uploadProgress} bytes]";
                                            //Log.Debug($"{prefix} {action.Message}");
                                        }
                                        catch (Exception ex) { Log.Fatal($"{prefix} Exception @ Upload Progress Callback: {ex.Message}"); }
                                    }
                                }

                            });

                            if (Cancellation.CheckTokenStatus(cancel, action)) { return; }

                            success = true;
                            action.Status = DeviceDeploymentActionStatus.SendingFileSuccess;
                            Log.Information($"{prefix} Sent File {remotefilepath} via SFTP");

                            SendConsoleCommand(postUploadCommand, action, cancel);
                        }
                        catch (Exception ex) { Log.Fatal($"{prefix} Exception Uploading File: {ex.Message}"); }
                        finally
                        {
                            fileStream.Close();
                            fileStream.Dispose();
                        }
                    }).Wait(cancel);
                }
                catch (Exception ex)
                {
                    action.Status = DeviceDeploymentActionStatus.SendingFileFailed;
                    action.Message = $"Failed to upload file via SFTP: {ex.Message}";
                    Log.Fatal($"{prefix} Failed to upload file via SFTP: {ex.Message}");
                }
            }

            return success;
        }

        /// <summary>
        /// handles the incoming data from a device that needs to be provisioned, and sends out the appropriate details
        /// </summary>
        /// <param name="data"></param>
        /// <param name="state"></param>
        /// <param name="action"></param>
        /// <param name="token"></param>
        private bool HandleProvisioningResponse(string user, string pass, ShellStream? stream, DeviceDeploymentAction action, CancellationToken token)
        {
            ProvisioningStatus state = ProvisioningStatus.NotStarted;

            if (stream != null)
            {
                int retries = 5;
                int attempt = 0;
                string data = String.Empty;

                Log.Debug($"{prefix} Stream: {(stream?.CanRead == true ? "Can Read" : "Cannot Read")} // {attempt} [Attempts] < {retries} [Retries] // {state} // Token: {(!token.IsCancellationRequested == true ? "Not Canceled" : "Canceled")}");

                while (stream?.CanRead == true && attempt < retries && state != ProvisioningStatus.Failure && state != ProvisioningStatus.Success)
                {
                    string? incoming = stream?.Read();

                    if (incoming != null && incoming != String.Empty) 
                    {
                        if (incoming + data != data) 
                        {
                            Log.Debug($"{prefix} {state} Attempt $:{attempt}");
                            Log.Debug($"{prefix} Buffer Contents: {data}");
                            UpdateResponse(data, action); 
                        } 
                    }

                    data += incoming;
                    data = TextHelpers.CleanString(data?.ToLower());

                    if (Regex.IsMatch(data, @"(?i)username:")) 
                    {
                        if (state == ProvisioningStatus.NotStarted) { attempt = 0; }
                        else if (state == ProvisioningStatus.WaitingPassword) { attempt++; }

                        state = ProvisioningStatus.WaitingPassword;
                        Log.Information($"{prefix} Attempting to create admin account with username: {user} on device -> {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} // {state} // {attempt}");
                        stream?.Write($"{user}\n"); 
                        data = String.Empty;
                        attempt = 0;
                    }
                    else if (Regex.IsMatch(data, @"(?i)[\s\S]*verify\s*password:")) 
                    {
                        if (state == ProvisioningStatus.WaitingPassword) { attempt = 0; }
                        else if (state == ProvisioningStatus.WaitingVerification) { attempt++; }

                        state = ProvisioningStatus.WaitingVerification;
                        Log.Information($"{prefix} Attempting to verify admin account password: {pass} for account named: {user} on device -> {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} // {state} // {attempt}");
                        stream?.Write($"{pass}\n"); 
                        data = String.Empty; 
                    }
                    else if (Regex.IsMatch(data, @"(?i)[\s\S]*password:")) 
                    {
                        if (state == ProvisioningStatus.WaitingVerification) { attempt = 0; }
                        else if (state == ProvisioningStatus.WaitingComplete) { attempt++; }

                        state = ProvisioningStatus.WaitingComplete;
                        Log.Information($"{prefix} Attempting to set admin account password: {pass} for account named: {user} on device -> {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} // {state} // {attempt}");
                        stream?.Write($"{pass}\n"); 
                        data = String.Empty; 
                    }
                    else if (Regex.IsMatch(data, @"(?i)[\s\S]*successfully\s*created.")) 
                    { 
                        state = ProvisioningStatus.Success;
                        Log.Information($"{prefix} Administrator account named: {user} with password: {pass} created on device -> {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} // {state} // {attempt}");
                    }

                    action.Message = state.ToString();
                }
            }
            else {
                Log.Error($"{prefix} SSH Stream For Provisioning {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} is null");
                action.Message = "SSH Stream Null! Unable To Provision Device";
            }
            
            return state == ProvisioningStatus.Success;
        }

        /// <summary>
        /// provisions the a new crestron device, assigning the administrator account
        /// </summary>
        /// <param name="user">the default username</param>
        /// <param name="pass">the default password</param>
        /// <param name="device">the crestron device</param>
        /// <param name="action">the deployment action that called this action</param>
        /// <param name="token">the cancellation token to allow the action to be stopped</param>
        /// <returns>the success response</returns>
        public bool ProvisionNewDevice(string user, string pass, DeviceDeploymentAction action, CancellationToken token)
        {
            bool success = false;

            ConnectionResult result = this.ConnectConsole(action);
            
            if (this.SshClient != null)
            {
                if (result == ConnectionResult.UseTelnet)
                {
                    action.Message = "Device does not support SSH, cannot provision device with default credentials";
                    Log.Warning($"{prefix} {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} -> {action.Message}");
                }
                else if (result == ConnectionResult.ConnectionFailure)
                {
                    action.Message = "Failed to connect to device, cannot provision device with default credentials";
                    Log.Warning($"{prefix} {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} -> {action.Message}");
                }
                else
                {
                    action.Message = "Device supports SSH, Opening Stream";
                    Log.Information($"{prefix} {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} -> opening shell stream");
                    try
                    {
                        ShellStream? stream = this.SshClient?.CreateShellStreamNoTerminal(1024);

                        success = HandleProvisioningResponse(user, pass, stream, action, token);
                    }
                    catch (SshConnectionException ex) 
                    { 
                        action.Message = ex.Message;
                        Log.Error($"{prefix} {this.NetworkConfiguration.Hostname} @ {this.NetworkConfiguration.IPAddress} -> SSH Connection Exception: {ex.Message}");
                    }
                }
            }

            return success;
        }

        public DeviceNetworkConfiguration GetCurrentNetworkConfiguration(CancellationToken cancel)
        {
            //run commands to update status
            string? response = this.SendConsoleCommandWithResponse("dhcp", cancel)?.ToLower();
            if (response != null) { Log.Debug($"{prefix} DHCP Status: {response}"); }

            if (response?.Contains("on") == true)
            {
                this.NetworkConfiguration.DHCP = true;

                response = this.SendConsoleCommandWithResponse("estatus", cancel)?.ToLower();

                Log.Debug($"{prefix} DHCP Ethernet Configuration: {response}");

                if (response != null)
                {
                    Match dhcpConfiguration = Regex.Match(response, this.NetworkConfigurationPattern);

                    this.NetworkConfiguration.IPAddress = NetworkValidators.NormalizeIPAddress(dhcpConfiguration.Groups["ipaddress"].Value);
                    this.NetworkConfiguration.Netmask = NetworkValidators.NormalizeIPAddress(dhcpConfiguration.Groups["subnet"].Value);
                    this.NetworkConfiguration.DefaultGateway = NetworkValidators.NormalizeIPAddress(dhcpConfiguration.Groups["gateway"].Value);
                    this.NetworkConfiguration.DnsServers.Add(NetworkValidators.NormalizeIPAddress(dhcpConfiguration.Groups["dns"].Value));
                }
            }
            else if (response?.Contains("off") == true)
            {
                this.NetworkConfiguration.DHCP = false;

                response = this.SendConsoleCommandWithResponse("ipaddr", cancel);

                if (response != null)
                {
                    //Log.Debug($"{prefix} IP Address Status: {response.ToLower()}");
                    if (Regex.IsMatch(response.ToLower(), @"[\d]+.[\d]+.[\d]+.[\d]+"))
                    {
                        Match ipAddr = Regex.Match(response.ToLower(), @"[\d]+.[\d]+.[\d]+.[\d]+");
                        string ip = ipAddr.Value;
                        Log.Debug($"{prefix} Static IP Address: {ip}");
                        this.NetworkConfiguration.IPAddress = NetworkValidators.NormalizeIPAddress(ip);
                    }
                }

                response = this.SendConsoleCommandWithResponse("listdns", cancel);
                if (response != null)
                {
                    Log.Debug($"{prefix} DNS Servers: {response}");

                    if (Regex.IsMatch(response.ToLower(), @"[\d]+.[\d]+.[\d]+.[\d]+"))
                    {
                        MatchCollection dns = Regex.Matches(response.ToLower(), @"[\d]+.[\d]+.[\d]+.[\d]+");
                        dns.ToList().ForEach(m => this.NetworkConfiguration.DnsServers.Add(NetworkValidators.NormalizeIPAddress(m.Value)));
                    }
                }

                response = this.SendConsoleCommandWithResponse("ipmask", cancel);
                if (response != null)
                {
                    Log.Debug($"{prefix} Subnet Mask: {response}");

                    if (Regex.IsMatch(response.ToLower(), @"[\d]+.[\d]+.[\d]+.[\d]+"))
                    {
                        Match ipAddr = Regex.Match(response.ToLower(), @"[\d]+.[\d]+.[\d]+.[\d]+");
                        string ip = ipAddr.Value;
                        Log.Debug($"{prefix} Subnet Mask: {ip}");
                        this.NetworkConfiguration.Netmask = NetworkValidators.NormalizeIPAddress(ip);
                    }
                }

                response = this.SendConsoleCommandWithResponse("defrouter", cancel);
                if (response != null)
                {
                    Log.Debug($"{prefix} Default Router Address: {response}");

                    if (Regex.IsMatch(response.ToLower(), @"[\d]+.[\d]+.[\d]+.[\d]+"))
                    {
                        Match ipAddr = Regex.Match(response.ToLower(), @"[\d]+.[\d]+.[\d]+.[\d]+");
                        string ip = ipAddr.Value;
                        Log.Debug($"{prefix} Default Router Address: {ip}");
                        this.NetworkConfiguration.DefaultGateway = NetworkValidators.NormalizeIPAddress(ip);
                    }
                }
            }

            return this.NetworkConfiguration;
        }

        public bool UpdateDnsServer(string server, DeviceDeploymentAction action, CancellationToken token)
        {
            bool result = false;
            string? dnsTable = String.Empty;

            ConnectionResult connection = this.ConnectConsole(action);
            if (connection == ConnectionResult.UseSsh) {
                SshCommand? dnsAvailable = this.SshClient?.RunCommand("listdns");
                dnsTable = dnsAvailable?.Result;
            }
            else { }

            return result;
        }
    }
}
