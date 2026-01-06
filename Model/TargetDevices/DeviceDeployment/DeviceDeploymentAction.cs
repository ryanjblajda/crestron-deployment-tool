using CrestronDeploymentTool.Utilities;
using Microsoft.Win32;
using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void AssignAction(Func<CancellationToken, bool> action)
        {
            Action = action;
        }

        public bool? Invoke(CancellationToken token)
        {
            return Action?.Invoke(token);
        }

        private string CleanString(string data)
        {
            return Regex.Replace(data, @"[^\x20-\x7E]", "");
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
                    device.TcpClient?.GetStream().Write(Encoding.ASCII.GetBytes($"{command}\r"));

                    if (device.TcpClient?.GetStream().CanRead == true)
                    {
                        byte[] buf = new byte[1024];
                        bool endOfResponseFound = false;

                        while (device.TcpClient?.Available != 0 && !endOfResponseFound)
                        {
                            device.TcpClient?.GetStream().Read(buf, 0, device.TcpClient.Available);
                            string incoming = Encoding.ASCII.GetString(buf);
                            action.Message = incoming;
                                
                            Debug.WriteLine($"{prefix} Incoming Telnet Data -> {action.CleanString(incoming)}");

                            if (incoming.Contains(">")) { 
                                endOfResponseFound = true;
                                Debug.WriteLine($"{prefix} End of Response Found (Bytes Available: {device.TcpClient?.Available}), Disconnecting");
                            }                            
                        }
                        success = true;
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
                    if (command != String.Empty)
                    {
                        action.Status = DeviceDeploymentActionStatus.Waiting;
                        Debug.WriteLine($"{prefix} Running Command: {command}");
                        SshCommand? cmd = device.SshClient?.CreateCommand(command);

                        if (cmd != null)
                        {
                            IAsyncResult? asyncResult = cmd?.BeginExecute();
                            action.Status = DeviceDeploymentActionStatus.SendingCommand;
                            if (asyncResult != null)
                            {
                                action.Status = DeviceDeploymentActionStatus.WaitingForResponse;

                                if (cmd != null)
                                {
                                    StreamReader output = new StreamReader(cmd.OutputStream);

                                    while (!asyncResult.IsCompleted)
                                    {
                                        while (!output.EndOfStream)
                                        {
                                            string? line = output.ReadLine();
                                            action.Status = DeviceDeploymentActionStatus.InProgress;

                                            if (line != null)
                                            {
                                                //replace the bs that the terminal gives us when its a new line
                                                line = line.Replace($"{device.Model}>", String.Empty);
                                                //if the line is now an empty string, ignore it
                                                if (line != String.Empty)
                                                {
                                                    //append until we hit 255 characters
                                                    if (action.Message.Length < 255) { action.Message += $"\r" + line; }
                                                    else { action.Message = line; }
                                                    Debug.WriteLine($"{prefix} Command Response Line: {line}");
                                                }
                                            }
                                            if (Cancellation.CheckTokenStatus(cancel, action)) return false;
                                        }
                                        if (Cancellation.CheckTokenStatus(cancel, action)) return false;
                                    }
                                }

                                cmd?.EndExecute(asyncResult);
                            }

                            action.Status = DeviceDeploymentActionStatus.SendingCommandSuccess;
                            success = true;
                        }
                        else { Debug.WriteLine($"{prefix} No Command Provided"); }

                        device.SshClient?.Disconnect();
                    }
                }
                catch (Exception ex)
                {
                    success = false;
                    action.Status = DeviceDeploymentActionStatus.SendingCommandFailed;
                    action.Message += $"\r{ex.Message}";
                }
            }
            else { action.Status = DeviceDeploymentActionStatus.CompleteFailure; action.Message = "SSH Client Null!"; }

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

                Debug.WriteLine($"{prefix} Attempting to Upload MemoryStream to {remotefilepath} on {device.Name} @ {device.IpAddress}");

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
                                //Debug.WriteLine($"{prefix} {action.Message}");
                            }
                        });

                        if (Cancellation.CheckTokenStatus(cancel, action)) return false;

                        Debug.WriteLine($"{prefix} Sent File via SFTP");

                        success = true;
                        action.Status = DeviceDeploymentActionStatus.SendingFileSuccess;

                    }
                    else { return false; }

                    device.SftpClient?.Disconnect();

                    SendCommandSsh(postUploadCommand, action, device, cancel);
                }
                catch (Exception ex)
                {
                    action.Status = DeviceDeploymentActionStatus.SendingFileFailed;
                    action.Message = $"Failed to upload file via SFTP: {ex.Message}";
                    Debug.WriteLine($"{prefix} Failed to upload file via SFTP: {ex.Message}");
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

            if (result == ConnectionResult.UseSsh) { 
                device.ConnectConsole(action);
                Debug.WriteLine($"{prefix} Opening Local File {localfilepath}");
                FileStream fileStream = File.OpenRead(localfilepath);
                Debug.WriteLine($"{prefix} Attempting to Upload {localfilepath} to {remotefilepath} on {device.Name} @ {device.IpAddress}");

                try
                {
                    device.SftpClient?.UploadFile(fileStream, remotefilepath, uploadProgress =>
                    {
                        if (Cancellation.CheckTokenStatus(cancel, action)) device.SftpClient.Disconnect();
                        else
                        {
                            action.Status = DeviceDeploymentActionStatus.SendingFile;
                            double percent = uploadProgress * 100 / (ulong)fileStream.Length;
                            action.Message = $"Upload Progress: {percent}% [{uploadProgress} bytes]";
                            //Debug.WriteLine($"{prefix} {action.Message}");
                        }
                    });

                    if (Cancellation.CheckTokenStatus(cancel, action)) return false;

                    Debug.WriteLine($"{prefix} Sent File via SFTP");
                    
                    success = true;
                    action.Status = DeviceDeploymentActionStatus.SendingFileSuccess;

                    device.SftpClient?.Disconnect();

                    SendCommandSsh(postUploadCommand, action, device, cancel);
                }
                catch (Exception ex) {
                    action.Status = DeviceDeploymentActionStatus.SendingFileFailed; 
                    action.Message = $"Failed to upload file via SFTP: {ex.Message}";
                    Debug.WriteLine($"{prefix} Failed to upload file via SFTP: {ex.Message}");
                }
            }

            return success;
        }
    }
}
