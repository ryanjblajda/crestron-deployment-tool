using CrestronDeploymentTool.Model.Deployment.ProvisioningState;
using CrestronDeploymentTool.Utilities;

using Renci.SshNet;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Serilog;

namespace CrestronDeploymentTool.Model.TargetDevices.DeviceDeployment
{
    /// <summary>
    /// a class to represent a deployment action a device should take
    /// </summary>
    public class DeviceDeploymentAction : INotifyPropertyChanged
    {
        private const string prefix = "DeviceDeploymentAction |";
        public string Name { get; private set; }
        public string Description { get; private set; }

        private string _response = String.Empty;
        public string Response
        {
            get { return _response; }
            internal set
            {
                if (value != _response)
                {
                    _response = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _message = string.Empty;
        public string Message
        {
            get { return _message; }
            internal set
            {
                if (value != _message)
                {
                    _message = value;

                    OnPropertyChanged();
                }
            }
        }

        private DeviceDeploymentActionStatus _status;
        public DeviceDeploymentActionStatus Status
        {
            get { return _status; }
            internal set
            {
                if (value != _status)
                {
                    _status = value;

                    OnPropertyChanged();
                }
            }
        }

        private Func<CancellationToken, bool>? Action { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public DeviceDeploymentAction(string name, string description)
        {
            Name = name;
            Message = "";
            Description = description;
            Status = DeviceDeploymentActionStatus.NotStarted;
        }

        /// <summary>
        /// the property changed event handler
        /// </summary>
        /// <param name="propertyName">the property that changed</param>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// assigns the function that should be called when the deployment action needs to be ran
        /// </summary>
        /// <param name="action">the function</param>
        public void AssignAction(Func<CancellationToken, bool> action)
        {
            Action = action;
        }

        /// <summary>
        /// invokes the function provided to this action
        /// </summary>
        /// <param name="token">the cancellation token to cancel the action</param>
        /// <returns>the result of the action</returns>
        public bool? Invoke(CancellationToken token)
        {
            return Action?.Invoke(token);
        }

        /// <summary>
        /// cleans a string of unusable characters
        /// </summary>
        /// <param name="data">the string to clean</param>
        /// <returns>the cleaned string</returns>
        private static string CleanString(string? data)
        {
            if (data != null) { return Regex.Replace(data, @"[^\x20-\x7E]", ""); }
            else { return String.Empty; }
        }

        /// <summary>
        /// updates the console response field with data incoming from the device
        /// </summary>
        /// <param name="line">the newest line of data</param>
        /// <param name="device">the device that sent the update</param>
        /// <param name="action">the action that called this</param>
        private static void UpdateResponse(string? line, CrestronDevice device, DeviceDeploymentAction action)
        {
            if (line != null)
            {
                //replace the bs that the terminal gives us when its a new line
                line = line.Replace($"{device.Model}>", String.Empty);
                line = line.Replace($"{device.Model.ToLower()}>", String.Empty);
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
        /// <param name="device">a reference to the device that the action should be performed on</param>
        /// <returns></returns>
        private static bool SendCommandTelnet(string command, DeviceDeploymentAction action, CrestronDevice device)
        {
            bool success = false;

            try
            {

                if (device.TcpClient != null)
                {
                    if (device.TcpClient?.Connected == false) { device.TcpClient?.Connect(IPAddress.Parse(device.IpAddress), Constants.TelnetPort); }
                    
                    Log.Debug($"{prefix} Running Command: {command} on {device.Name} @ {device.IpAddress} via Telnet");
                    
                    device.TcpClient?.GetStream().Write(Encoding.ASCII.GetBytes($"{command}\r"));
                    
                    action.Message = "Command Sent";
                    action.Status = DeviceDeploymentActionStatus.WaitingForResponse;

                    if (device.TcpClient?.GetStream().CanRead == true)
                    {
                        bool endOfResponseFound = false;
                        bool firstEndOfResponse = false;

                        while (device.TcpClient?.Connected == true && !endOfResponseFound)
                        {
                            byte[] buf = new byte[1024];
                            device.TcpClient?.GetStream().Read(buf, 0, device.TcpClient.Available);
                            string incoming = Encoding.ASCII.GetString(buf);
                            
                            UpdateResponse(incoming, device, action);

                            Log.Debug($"{prefix} Incoming Telnet Data -> {CleanString(incoming)}");

                            if (incoming.Contains(">"))
                            {
                                if (firstEndOfResponse) {
                                    endOfResponseFound = true;
                                    Log.Debug($"{prefix} End of Response Found (Bytes Available: {device.TcpClient?.Available}), Disconnecting");
                                }
                                else {
                                    firstEndOfResponse = true;
                                    Log.Debug($"{prefix} First End of Response Found, Staying Connected...");
                                }
                            }
                        }

                        success = true;
                        action.Status = DeviceDeploymentActionStatus.SendingCommandSuccess;
                    }
                }
            }
            catch (Exception ex) { action.Message = ex.Message; }

            return success;
        }

        /// <summary>
        /// sends a command to a device via SSH and waits for a response
        /// </summary>
        /// <param name="command">the command to send</param>
        /// <param name="action">a reference to the action that called the function</param>
        /// <param name="device">a reference to the device that the action should be performed on</param>
        /// <param name="cancel">a cancellation token to listen to so that this action can be cancelled if needed</param>
        /// <returns></returns>
        private static bool SendCommandSsh(string command, DeviceDeploymentAction action, CrestronDevice device, CancellationToken cancel)
        {
            bool success = false;

            if (device.SshClient != null)
            {
                try
                {
                    action.Status = DeviceDeploymentActionStatus.SendingCommand;

                    if (command != String.Empty)
                    {
                        action.Status = DeviceDeploymentActionStatus.Waiting;
                        Log.Debug($"{prefix} Running Command: {command} on {device.Name} @ {device.IpAddress} via SSH");
                        SshCommand? cmd = device.SshClient.RunCommand(command + "\r");
                        action.Message = "Command Sent";

                        if (cmd != null)
                        {
                            action.Status = DeviceDeploymentActionStatus.WaitingForResponse;

                            Log.Debug($"{prefix} {device.Name} @ {device.IpAddress} SSH Command Result: {CleanString(cmd?.Result)} {(cmd?.Error == String.Empty ? "" : " // Error:" + cmd?.Error)} {(cmd?.ExitStatus == null ? "" : " // Exit Status:" + cmd?.ExitStatus)}");

                            if (cmd?.Result != null) { UpdateResponse(cmd.Result, device, action); }
                            
                            if (cmd?.Error != null) { UpdateResponse(cmd?.Error, device, action); }

                            if (cmd?.ExitStatus == 0 || cmd?.Error == String.Empty)
                            { 
                                Log.Information($"{prefix} Command {command} Successfully sent to {device.Name} @ {device.IpAddress}");
                                action.Status = DeviceDeploymentActionStatus.SendingCommandSuccess;
                                success = true;
                            }
                            else {
                                Log.Information($"{prefix} Command {command} Failure sending to {device.Name} @ {device.IpAddress}");
                                action.Status = DeviceDeploymentActionStatus.SendingCommandFailed; 
                        }
                    }
                        else { Log.Warning($"{prefix} Command is null!"); }
                }
                    else { Log.Debug($"{prefix} Command is empty string!!"); }
                }
                catch (Exception ex)
                {
                    success = false;
                    action.Status = DeviceDeploymentActionStatus.SendingCommandFailed;
                    action.Message += $"\r{ex.Message}";
                    Log.Fatal($"{prefix} {ex.Message}");
                }
            }
            else { 
                action.Status = DeviceDeploymentActionStatus.CompleteFailure; 
                action.Message = "SSH Client Null!";
                Log.Warning($"{prefix} {action.Message}");
            }

            return success;
        }

        /// <summary>
        /// sends a console command to a device
        /// </summary>
        /// <param name="command">the command to send</param>
        /// <param name="action">a reference to the action that called the function</param>
        /// <param name="device">a reference to the device that the action should be performed on</param>
        /// <param name="cancel">a cancellation token to listen to so that this action can be cancelled if needed</param>
        /// <returns>a bool representing whether or not the command was sent to the device, not necessarily whether the command has a positive response</returns>
        public static bool SendConsoleCommand(string command, CrestronDevice device, DeviceDeploymentAction action, CancellationToken cancel)
        {
            if (Cancellation.CheckTokenStatus(cancel, action)) return false;

            bool success = false;

            ConnectionResult connection = device.ConnectConsole(action);

            switch (connection)
            {
                case ConnectionResult.UseSsh:
                    success = SendCommandSsh(command, action, device, cancel);
                    break;
                case ConnectionResult.UseTelnet:
                    success = SendCommandTelnet(command, action, device);
                    break;
            }

            return success;
        }

        /// <summary>
        /// sends a file via memory stream to a device [zig file]
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="remotefilepath"></param>
        /// <param name="postUploadCommand"></param>
        /// <param name="device">a reference to the device that the action should be performed on</param>
        /// <param name="action">a reference to the action that called the function</param>
        /// <param name="cancel">a cancellation token to listen to so that this action can be cancelled if needed</param>
        /// <returns>a bool representing if the file was sent to the device, and the command sent (if provided) </returns>
        public static bool SendFileViaFTP(MemoryStream? stream, string remotefilepath, string postUploadCommand, CrestronDevice device, DeviceDeploymentAction action, CancellationToken cancel)
        {
            if (Cancellation.CheckTokenStatus(cancel, action)) return false;

            bool success = false;
            ConnectionResult result = ConnectionResult.ConnectionFailure;

            result = device.ConnectSFTP(action);

            if (result == ConnectionResult.UseSsh)
            {
                device.ConnectConsole(action);

                Log.Information($"{prefix} Attempting to Upload MemoryStream to {remotefilepath} on {device.Name} @ {device.IpAddress}");

                try
                {
                    //reset stream
                    stream?.Seek(0, SeekOrigin.Begin);
                    //try to upload
                    if (stream != null)
                    {
                        device.SftpClient?.UploadFile(stream, remotefilepath, uploadProgress =>
                        {
                            if (Cancellation.CheckTokenStatus(cancel, action)) device.SftpClient.Disconnect();
                            else
                            {
                                action.Status = DeviceDeploymentActionStatus.SendingFile;
                                double percent = uploadProgress * 100 / (ulong)stream.Length;
                                action.Message = $"Upload Progress: {percent}% [{uploadProgress} bytes]";
                                //Log.Debug($"{prefix} {action.Message}");
                            }
                        });

                        if (Cancellation.CheckTokenStatus(cancel, action)) { return false; };

                        success = true;
                        action.Status = DeviceDeploymentActionStatus.SendingFileSuccess;
                        Log.Information($"{prefix} Sent File {remotefilepath} via SFTP");

                        SendCommandSsh(postUploadCommand, action, device, cancel);
                    }
                    else
                    {
                        Log.Error($"{prefix} MemoryStream for {remotefilepath} is null!! Unable to upload to {device.Name} @ {device.IpAddress}");
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
        /// <param name="device">a reference to the device that the action should be performed on</param>
        /// <param name="action">a reference to the action that called the function</param>
        /// <param name="cancel">a cancellation token to listen to so that this action can be cancelled if needed</param>
        /// <returns>a bool representing if the file was sent to the device, and the command sent (if provided) </returns>
        public static bool SendFileViaFTP(string localfilepath, string remotefilepath, string postUploadCommand, CrestronDevice device, DeviceDeploymentAction action, CancellationToken cancel)
        {
            if (Cancellation.CheckTokenStatus(cancel, action)) return false;

            bool success = false;
            ConnectionResult result = ConnectionResult.ConnectionFailure;

            result = device.ConnectSFTP(action);

            if (result == ConnectionResult.UseSsh)
            {
                device.ConnectConsole(action);

                try
                {
                    Task.Run(() =>
                    {
                        Log.Debug($"{prefix} Opening Local File {localfilepath}");
                        FileStream fileStream = File.OpenRead(localfilepath);
                        Log.Information($"{prefix} Attempting to Upload {localfilepath} to {remotefilepath} on {device.Name} @ {device.IpAddress}");

                        try {
                            device.SftpClient?.UploadFile(fileStream, remotefilepath, uploadProgress =>
                            {
                                if (device != null)
                                {
                                    if (Cancellation.CheckTokenStatus(cancel, action))
                                    {
                                        Log.Warning($"{prefix} Upload Canceled");
                                        if (device.SftpClient != null) { device.SftpClient?.Disconnect(); }
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

                                SendCommandSsh(postUploadCommand, action, device, cancel);
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
        /// <param name="device"></param>
        /// <param name="action"></param>
        /// <param name="token"></param>
        private static ProvisioningStatus HandleProvisioningResponse(string? data, string user, string pass, ref int attempt, ShellStream? stream, ProvisioningStatus state, CrestronDevice device, DeviceDeploymentAction action, CancellationToken token)
        {
            if (stream != null)
            {
                if (data != null)
                {
                    if (data != String.Empty)
                    {
                        Log.Debug($"{prefix} Received: {data}");

                        if (state == ProvisioningStatus.WaitingUsername && Regex.IsMatch(data, @"(?i)username\s*:\s*$"))
                        {
                            Log.Information($"Attempting to create admin account with username: {user} on device -> {device.Name} @ {device.IpAddress}");
                            stream?.Write($"{user}\r");
                            state = ProvisioningStatus.WaitingPassword;
                            attempt++;
                        }
                        
                        if (state == ProvisioningStatus.WaitingPassword && Regex.IsMatch(data, @"(?i)password\s*:\s*$"))
                        {
                            Log.Information($"Attempting to set admin account password: {pass} for account named: {user} on device -> {device.Name} @ {device.IpAddress}");
                            stream?.Write($"{pass}\r");
                            state = ProvisioningStatus.WaitingVerification;
                            attempt++;
                        }
                        
                        if (state == ProvisioningStatus.WaitingVerification && Regex.IsMatch(data, @"(?i)verify\s+password\s*:\s*$"))
                        {
                            Log.Information($"Attempting to verify admin account password: {pass} for account named: {user} on device -> {device.Name} @ {device.IpAddress}");
                            stream?.Write($"{pass}\r");
                            state = ProvisioningStatus.Complete;
                            attempt++;
                        }
                        
                        if (state == ProvisioningStatus.Complete && Regex.IsMatch(data, @"(?i)successfully\s+created."))
                        {
                            Log.Information($"Administrator account named: {user} with password: {pass} created on device -> {device.Name} @ {device.IpAddress}");
                            state = ProvisioningStatus.Success;
                        }
                        
                        if (data.Contains("error"))
                        {
                            action.Message += data;
                            state = ProvisioningStatus.Failure;
                        }
                    }
                }
            }

            return state;
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
        public static bool ProvisionNewDevice(string user, string pass, CrestronDevice device, DeviceDeploymentAction action, CancellationToken token)
        {
            bool success = false;
            int retries = 4;

            ConnectionResult result = device.ConnectConsole(action);
            if (device.SshClient != null)
            {
                if (result == ConnectionResult.UseTelnet) { 
                    action.Message = "Device does not support SSH, cannot provision device with default credentials";
                    Log.Warning($"{prefix} {action.Message}");
                }
                else if (result == ConnectionResult.ConnectionFailure) { 
                    action.Message = "Failed to connect to device, cannot provision device with default credentials";
                    Log.Warning($"{prefix} {action.Message}");
                }
                else
                {
                    ProvisioningStatus state = ProvisioningStatus.WaitingUsername;
                    int attempt = 0;

                    if (device.SshClient?.IsConnected != true) { device.SshClient?.Connect(); }

                    ShellStream? stream = device.SshClient?.CreateShellStreamNoTerminal(1024);
                    stream?.Write("\r");
                    string? data = String.Empty;

                    while (stream?.CanRead == true && attempt < retries && state != ProvisioningStatus.Failure && state != ProvisioningStatus.Success)
                    {
                        if (token.IsCancellationRequested) { break; }

                        string? incoming = stream?.Read();
                        
                        if (incoming != null) { if (incoming + data != data) { UpdateResponse(data, device, action); } }

                        data += incoming;
                        data = data?.ToLower();

                        Log.Debug($"{prefix} {data}");

                        ProvisioningStatus update = HandleProvisioningResponse(data, user, pass, ref attempt, stream, state, device, action, token);
                        
                        if (update != state)
                        {
                            Log.Debug($"{prefix} Attempt: {attempt} -> {update}");
                            attempt = 0;
                            action.Message = $"{update}";
                            data = String.Empty;
                            state = update;
                        }

                        if (update == ProvisioningStatus.Success) { success = true; }
                    }

                    Log.Information($"{prefix} Disconnecting from {device.Name} @ {device.IpAddress}");
                }
            }

            return success;
        }
    }
}
