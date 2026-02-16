using CrestronDeploymentTool.UserInterface;
using CrestronDeploymentTool.Model.TargetDevices;
using CrestronDeploymentTool.Model.Deployment.ConsoleCommand;
using CrestronDeploymentTool.Model.TargetDevices.DeviceDeployment;
using CrestronDeploymentTool.Utilities;

using System.Windows;
using System.Collections.Immutable;
using Microsoft.Win32;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using Serilog;

namespace CrestronDeploymentTool.Model.Deployment
{
    public static class DeploymentWizard
    {
        private static Window? mainWindow;
        private const string prefix = "DeploymentWizard |";

        /// <summary>
        /// asks the user for connection details requiring an entry before continuing
        /// </summary>
        /// <param name="owner">the window owner</param>
        /// <param name="prompt">the string prompt to provide to the end user if needed</param>
        /// <returns></returns>
        public static (string, string, bool) ConnectionDetails(Window owner, string? prompt = null)
        {
            mainWindow = owner;

            string username = String.Empty, password = String.Empty;

            bool? result = false;
            bool cancelDeployment = false;

            while (result == false)
            {
                ConnectionCredentialsDialog dialog;

                if (prompt == null) { dialog = new ConnectionCredentialsDialog(owner); }
                else { dialog = new ConnectionCredentialsDialog(owner, prompt); }
                
                result = dialog.ShowDialog();

                if (result == true)
                {
                    username = dialog.Username.Text;
                    password = dialog.Password.Password;
                }
                else 
                {
                    MessageBoxResult why = ConfirmationDialog.Show("You must enter credentials....how else can I connect to the devices?", "Provide Credentials (Pretty Please)", MessageBoxButton.OKCancel);
                    //old version using default messagebox
                    //MessageBoxResult why = MessageBox.Show("You must enter credentials....how else can I connect to the devices?", "Provide Credentials (Pretty Please)", MessageBoxButton.OKCancel); 
                    
                    if (why == MessageBoxResult.Cancel) 
                    { 
                        result = true;
                        cancelDeployment = true;
                    }
                }
            }

            return (username, password, cancelDeployment);
        }

        /// <summary>
        /// creates a prompt and shows it to the end user in order to re-select devices to be targeted by the action
        /// </summary>
        /// <param name="action">the action to be performed against the device</param>
        /// <param name="filtered">the filtered list of devices</param>
        /// <returns>the selected list of devices from the filter</returns>
        private static List<CrestronDevice> PromptForTargetDevices(string action, List<string> filtered)
        {
            List<CrestronDevice> actionTargetDevices = new List<CrestronDevice>();

            SelectTargetDevicesDialog dialog = new SelectTargetDevicesDialog(mainWindow, action, filtered);

            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                foreach (string item in dialog.TargetDevices.SelectedItems)
                {
                    if (DiscoveredDevices.SelectedTargetDevices.Any(d => d.NetworkConfiguration.Hostname == item))
                    {
                        actionTargetDevices.Add(DiscoveredDevices.SelectedTargetDevices.First(d => d.NetworkConfiguration.Hostname == item));
                    }
                }
            }
            else { actionTargetDevices = DiscoveredDevices.SelectedTargetDevices.ToList(); }

            return actionTargetDevices;
        }

        /// <summary>
        /// filter the discovered or manually entered devices by whether or not they are capable of completing the action
        /// </summary>
        /// <param name="prompt">should a new prompt be provided to the user to re-select from valid devices</param>
        /// <param name="action">the action that will be performed on the device</param>
        /// <param name="filter">a list of valid model prefixes that we should use to filter the discovered devices with</param>
        /// <returns>the filtered list of devices</returns>
        private static List<CrestronDevice> DetermineTargetDevices(bool prompt, string action, ImmutableList<string>? filter = null)
        {
            //prompt for target devices -> dialog
            List<CrestronDevice> devices = new List<CrestronDevice>();
            List<string> filtered = new List<string>();
            
            //filter out devices incapable of what the current deployment action requires if needed
            if (filter != null) { filtered = DiscoveredDevices.SelectedTargetDevices.Where(d => filter.Any(prefix => d.Model.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))).ToList().Select(d => d.NetworkConfiguration.Hostname).ToList(); }
            //if not, get em all
            else { filtered = DiscoveredDevices.SelectedTargetDevices.Select(i => i.NetworkConfiguration.Hostname).ToList(); }

            //if we need to prompt for selection we provide the list with this specific selection
            if (prompt)
            {
                //we only want to show the prompt, if there is a device to select
                if (filtered.Count > 0) { devices = PromptForTargetDevices(action, filtered); }
                else
                {
                    ConfirmationDialog.Show($"There are no devices available from your initial selection that support {action}.", "Notice");
                    //old version using default messagebox
                    //MessageBox.Show($"There are no devices available from your initial selection that support {action}."); }
                }
            }
            //if we didnt need to prompt, get all currently selected devices
            else { devices = DiscoveredDevices.SelectedTargetDevices.ToList(); }

            return devices;
        }

        /// <summary>
        /// prompts the user to determine whether or not commands should be sent in batch mode, or unique mode
        /// </summary>
        /// <returns>the send type</returns>
        private static ConsoleCommandSendType DetermineConsoleCommandSendType()
        {
            ConsoleCommandSendType type = ConsoleCommandSendType.Batch;

            MessageBoxResult confirm = MessageBoxResult.No, result = MessageBoxResult.None;
            
            while (confirm != MessageBoxResult.Yes) {
                result = ConfirmationDialog.Show("By default, we will send the *same command to all devices* aka ***batch*** mode.\r\rWould you like to send commands in *unique* mode instead?\r\r**(This will prompt you for a command per-device)**", "Command Send Mode", MessageBoxButton.YesNo);

                if (result == MessageBoxResult.None || confirm == MessageBoxResult.None) { ConfirmationDialog.Show("You *must* make a selection.\r\rIf you **really** want to cancel the operation you can do this per-device later in the wizard.", "Selection Required", MessageBoxButton.OK); }
                else if (result == MessageBoxResult.Yes) { type = ConsoleCommandSendType.Unique; }
                else if (result == MessageBoxResult.No) { type = ConsoleCommandSendType.Batch; }

                if (result == MessageBoxResult.Yes || result == MessageBoxResult.No) { confirm = ConfirmationDialog.Show($"You have elected to send commands in **{type.ToString().ToLower()}** mode\r\r**Is this correct?**", "Confirm Selection", MessageBoxButton.YesNo); }
            }
                    
            return type;
        }

        /// <summary>
        /// a wizard for guiding the user through the deployment of console commands to a device, in either "batch" mode or "unique" mode
        /// </summary>
        /// <param name="prompt">whether or not the user should be re-prompted to select a more specific set of devices</param>
        /// <returns>whether or the operation was canceled during the wizard configuration process</returns>
        public static bool ConsoleCommands(bool prompt)
        {
            bool canceled = false;
            
            List<CrestronDevice> targets = DetermineTargetDevices(prompt, DeploymentWizardActions.SendConsoleCommands);

            ConsoleCommandSendType type = DetermineConsoleCommandSendType();

            if (type == ConsoleCommandSendType.Batch) 
            {
                bool? result = false;
                string command = String.Empty;

                while (result == false)
                {
                    SimpleTextEntryDialog dialog = new SimpleTextEntryDialog("Please provide the console command that will be sent to *all* devices", "Console Command", "Console Command Entry", @"^[a-zA-Z_][a-zA-Z0-9_-]*(\s+(""[^""]*""|'[^']*'|[^\s""']+))*$", mainWindow);
                    result = dialog.ShowDialog();
                    
                    if (result != true) {
                        MessageBoxResult cancel = ConfirmationDialog.Show("Look man, if you wanted to send console commands you can't provide me an empty string. Either provide me a command or cancel the operation.", "Console Command Cannot Be Empty!", MessageBoxButton.OKCancel);
                        //old version using default messagebox
                        //MessageBoxResult cancel = MessageBox.Show("Look man, if you wanted to send console commands you can't provide me an empty string. Either provide me a command or cancel the operation.", "Console Command Cannot Be Empty!", MessageBoxButton.OKCancel);
                        
                        if (cancel == MessageBoxResult.Cancel) { return(canceled = true); }
                    }

                    command = dialog.TextEntered.Text;
                }

                if (result == true) 
                { 
                    targets.ForEach(d => 
                    {
                        //create a new action
                        DeviceDeploymentAction action = new DeviceDeploymentAction(DeploymentWizardActions.SendConsoleCommands, $"Perform {command} command on {d.NetworkConfiguration.Hostname} @ {d.NetworkConfiguration.Hostname}");
                        //assign a lamba function wrapping the send console command call, passing in the command, the device we need to perform the action on, and a reference to the action so that it can be updated with status
                        action.AssignAction((token) => { return d.SendConsoleCommand(command, action, token); });
                        //assign that action to the device for later use
                        d.DeploymentActions.Add(action);
                    }); 
                }
            }
            else if (type == ConsoleCommandSendType.Unique) 
            {
                targets.ForEach(d =>
                {
                    string command = String.Empty;
                    bool? result = false;
                    bool canceled = false;

                    while (result == false)
                    {
                        SimpleTextEntryDialog dialog = new SimpleTextEntryDialog($"Please provide the console command that will be sent to {d.NetworkConfiguration.Hostname} @ {d.NetworkConfiguration.Hostname}", $"{d.NetworkConfiguration.Hostname} Console Command", "Console Command Entry", @"^[a-zA-Z_][a-zA-Z0-9_-]*(\s+(""[^""]*""|'[^']*'|[^\s""']+))*$", mainWindow);
                        result = dialog.ShowDialog();

                        if (result != true)
                        {
                            MessageBoxResult cancel = ConfirmationDialog.Show("Look man, if you wanted to send a console command you can't provide me an empty string.\r\r*Either provide me a command or cancel the operation.*", "Console Command Cannot Be Empty!", MessageBoxButton.OKCancel);
                            //old version using default messagebox
                            //MessageBoxResult cancel = MessageBox.Show("Look man, if you wanted to send console commands you can't provide me an empty string. Either provide me a command or cancel the operation.", "Console Command Cannot Be Empty!", MessageBoxButton.OKCancel);

                            if (cancel == MessageBoxResult.Cancel) { canceled = true; result = true; }
                        }

                        command = dialog.TextEntered.Text;
                    }

                    if (!canceled)
                    {
                        //create a new action
                        DeviceDeploymentAction action = new DeviceDeploymentAction(DeploymentWizardActions.SendConsoleCommands, $"Perform {command} command on {d.NetworkConfiguration.Hostname} @ {d.NetworkConfiguration.Hostname}");
                        //assign a lamba function wrapping the send console command call, passing in the command, the device we need to perform the action on, and a reference to the action so that it can be updated with status
                        action.AssignAction((token) => { return d.SendConsoleCommand(command, action, token); });
                        //assign that action to the device for later use
                        d.DeploymentActions.Add(action);
                    }
                });
            }
            return (canceled);
        }

        /// <summary>
        /// using the program file location, get the local sig file and send it to zip file (renamed to zig)
        /// </summary>
        /// <param name="programfile"></param>
        /// <returns></returns>
        private static MemoryStream? CreateZIGFile(string programfile)
        {
            //create a memory stream to hold the zip archive in memory
            MemoryStream zigStream = new MemoryStream();
            //represent the zip archive
            ZipArchive zigFile = new ZipArchive(zigStream, ZipArchiveMode.Create);
            //change the extension to get the sig file
            string sigFile = Path.ChangeExtension(programfile, "sig");
            //make sure the path actually exists
            if (Path.Exists(sigFile)) { zigFile.CreateEntryFromFile(sigFile, Path.GetFileName(sigFile)); }
            //return null if the path doesnt exist
            else { return null; }
            //return the object
            return zigStream;
        }

        /// <summary>
        /// a wizard for guiding the user through the deployment of programming files to a processors programming folder
        /// </summary>
        /// <param name="prompt">whether or not the user should be re-prompted to select a more specific set of devices</param>
        /// <returns>whether or the operation was canceled during the wizard configuration process</returns>
        public static bool Programming(bool prompt)
        {
            bool canceled = false;
            //prompt for target devices
            List<CrestronDevice> targetDevices = DetermineTargetDevices(prompt, DeploymentWizardActions.SendProgramming, DiscoveredDevices.ProgrammingCapableDevices);
            //create a list of canceled devices
            List<CrestronDevice> canceledDevices = new List<CrestronDevice>();
            //loop through each target device to determine what configuration file should be sent
            targetDevices.ForEach(device =>
            {
                bool confirmed = false;
                bool canceled = false;
                string localFilePath = String.Empty;
                int programSlot = 1;
                bool? includeSIGFile = true;
                bool? overwriteIPTable = false;

                //make sure user confirms or cancels the operation
                while (!confirmed && !canceled)
                {
                    //open file select dialog
                    string crestronCompiledFilter = "Crestron Compiled Programs (*.cpz;*.spz;*.lpz)|*.cpz;*.spz;*.lpz|" + "SIMPLSharp Pro (*.cpz)|*.cpz|" + "2-Series Programs (*.spz)|*.spz|" + "3-Series Programs (*.lpz)|*.lpz";
                    OpenFileDialog dialog = new OpenFileDialog { Title = "Select Program File", Filter = crestronCompiledFilter, InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)};
                    bool? result = dialog.ShowDialog();
                    localFilePath = dialog.FileName;

                    if (result == true)
                    {
                        MessageBoxResult confirmSelection = ConfirmationDialog.Show($"You have selected the following program:\r\r**{Path.GetFileName(localFilePath)}**", "Confirm Program File Selection", MessageBoxButton.YesNo);
                        //old version using default messagebox
                        //MessageBoxResult confirmSelection = MessageBox.Show($"You have selected the following program:\r\r**{Path.GetFileName(localFilePath)}**", "Confirm Program File Selection", MessageBoxButton.YesNo);
                        if (confirmSelection == MessageBoxResult.Yes) 
                        { 
                            confirmed = true;

                            bool? programOptionsConfirmed = false;
                            
                            while(programOptionsConfirmed == false) 
                            {
                                ProgrammingOptions progOptionsDialog = new ProgrammingOptions(Path.GetFileName(localFilePath), device.NetworkConfiguration.Hostname);
                                bool? exited = progOptionsDialog.ShowDialog();
                                programSlot = (int)progOptionsDialog.ProgramSlot.SelectedItem;
                                includeSIGFile = progOptionsDialog.IncludeSIGFile.IsChecked;
                                overwriteIPTable = progOptionsDialog.OverwriteIPTable.IsChecked;

                                if (exited == false) {
                                    ConfirmationDialog.Show($"Listen here, you *gotta* select program options, since you decided to send ***{Path.GetFileName(localFilePath)}*** to {device.NetworkConfiguration.Hostname}.", "Program Options Required", MessageBoxButton.OK);
                                    //old version using default messagebox
                                    //MessageBoxResult cancelOperation = MessageBox.Show($"Listen here, you gotta select program options, since you decided to send {Path.GetFileName(localFilePath)} to {device.NetworkConfiguration.Hostname}.", "Program Options Required", MessageBoxButton.OK); 
                                }
                                else {
                                    string programOptionMessage = $"You have selected the following options for the deployment of: *{Path.GetFileName(localFilePath)}* to *{device.NetworkConfiguration.Hostname}*\r\r" +
                                    $"Program Slot: *{programSlot}*\r" +
                                    $"Include Sig File: *{(progOptionsDialog.IncludeSIGFile.IsChecked == true ? "Yes" : "No")}*\r" +
                                    $"Overwrite IP Table: *{(progOptionsDialog.OverwriteIPTable.IsChecked == true ? "Yes" : "No")}*\r\r" +
                                    $"***Are these options correct?***";
                                    MessageBoxResult optionsConfirmedResponse = ConfirmationDialog.Show(programOptionMessage, "Confirm Program Options", MessageBoxButton.YesNo);
                                    if (optionsConfirmedResponse == MessageBoxResult.Yes) { programOptionsConfirmed = true; }
                                }
                            }
                        }
                    }
                    else
                    {
                        MessageBoxResult cancelOperation = ConfirmationDialog.Show($"Listen here, you *gotta* select a program if you want me to send it. Do you want to send a new program to {device.NetworkConfiguration.Hostname} or not?", "Cancel Program Update", MessageBoxButton.YesNo);
                        //old version using default messagebox
                        //MessageBoxResult cancelOperation = MessageBox.Show($"Listen here, you gotta select a program if you want me to send it. Do you want to send a new program to {device.NetworkConfiguration.Hostname} or not?", "Cancel Program Update", MessageBoxButton.YesNo);
                        if (cancelOperation == MessageBoxResult.No) { canceled = true; }
                    }
                }

                if (canceled) { canceledDevices.Add(device); }

                if (confirmed)
                {
                    DeviceDeploymentAction stopProgram = new DeviceDeploymentAction(DeploymentWizardActions.SendConsoleCommands, "Stop Running Program");
                    stopProgram.AssignAction((token) => { return device.SendConsoleCommand("stopprog -p:1", stopProgram, token); });
                    device.DeploymentActions.Add(stopProgram);

                    string message = $"Upload {Path.GetFileName(localFilePath)} to slot {programSlot}, {(overwriteIPTable == true ? "updating IP table" : "")} {(includeSIGFile == true ? "sending sig file" : "")}";
                    string postUploadCommand = $"progload -p:{programSlot}";

                    //make sure to add the no flag if we have not elected to overwrite the ip table (or somehow the value is null, because that should be the default operation)
                    if (overwriteIPTable == false) { postUploadCommand += "-n";  }

                    DeviceDeploymentAction action = new DeviceDeploymentAction(DeploymentWizardActions.SendProgramming, message);
                    
                    action.AssignAction((token) => { return device.SendFileViaFTP(localFilePath, $"/program{programSlot:D2}/{Path.GetFileName(localFilePath)}", postUploadCommand, action, token); });
                    device.DeploymentActions.Add(action);

                    //if we need to upload the sig file, we will need to add a deployment action
                    if (includeSIGFile == true) {
                        MemoryStream? zigArchive = CreateZIGFile(localFilePath);
                        
                        if (zigArchive != null) {
                            DeviceDeploymentAction sigAction = new DeviceDeploymentAction("Send Sig File", $"Upload Sig File for {Path.GetFileName(localFilePath)}");
                            sigAction.AssignAction((token) => { return device.SendFileViaFTP(zigArchive, $"/program{programSlot:D2}/{Path.GetFileNameWithoutExtension(localFilePath)}.zig", "", sigAction, token); });
                            device.DeploymentActions.Add(sigAction);
                        }
                    }
                }
            });

            if (targetDevices.Count == canceledDevices.Count) { canceled = true; }

            return canceled;
        }

        /// <summary>
        /// a wizard for guiding the user through the deployment of configuration files to a interfaces /display/ folder
        /// </summary>
        /// <param name="prompt">whether or not the user should be re-prompted to select a more specific set of devices</param>
        /// <returns>whether or the operation was canceled during the wizard configuration process</returns>
        public static bool UserInterfaces(bool prompt)
        {
            bool fileUploadCanceled = false, ipTableUpdateCanceled = false;
            //prompt for target devices [filter by touchpanel for now] -- add support for processors in the future
            List<CrestronDevice> targetDevices = DetermineTargetDevices(prompt, DeploymentWizardActions.SendUserInterfaces, DiscoveredDevices.UserInterfaceCapableDevices);

            fileUploadCanceled = DeploymentWizard.UserInterfaceFiles(targetDevices);

            if (targetDevices.Count > 0)
            {
                MessageBoxResult result = ConfirmationDialog.Show("Do you want to update/configure IP Table entries?", "Update IP Table Entries", MessageBoxButton.YesNo);
                //old version using default messagebox
                //MessageBoxResult result = MessageBox.Show("Do you want to update/configure IP Table entries?", "Update IP Table Entries", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes) { DeploymentWizard.UserInterfaceIPTable(targetDevices); }
            }

            return (fileUploadCanceled && ipTableUpdateCanceled);
        }

        /// <summary>
        /// a subwizard for guiding the user through add an ip entry to the available devices
        /// </summary>
        /// <param name="targetDevices">a list of valid devices to be targeted by this type of operation</param>
        /// <returns>whether or the operation was canceled during the wizard configuration process</returns>
        private static bool UserInterfaceIPTable(List<CrestronDevice> targetDevices)
        {
            bool canceled = false;

            List<CrestronDevice> canceledDevices = new List<CrestronDevice>();
            //loop through each target device to determine what configuration file should be sent
            targetDevices.ForEach(device =>
            {
                bool confirmed = false;
                bool canceled = false;
                string newIPID = String.Empty;
                string newParent = String.Empty;
                //make sure user confirms or cancels the operation
                while (!confirmed && !canceled)
                {
                    //open dual text entry dialog
                    string validNewParentPattern = @"^((?:(?:25[0-5]|2[0-4]\d|1\d{2}|[1-9]?\d)\.){3}(?:25[0-5]|2[0-4]\d|1\d{2}|[1-9]?\d)|(?=.{1,253}$)(?:(?:[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)*[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?))$";
                    SimpleDualTextEntryDialog dialog = new SimpleDualTextEntryDialog($"Please provide the new IP ID for *{device.NetworkConfiguration.Hostname}* in *HEXADECIMAL*", "IP ID Entry", "Processor IP Address/Hostname", "Updated IP ID", @"^(?:0[1-9A-Fa-f]|[1-9A-Fa-f][0-9A-Fa-f])$", validNewParentPattern, mainWindow);
                    bool? result = dialog.ShowDialog();
                    newIPID = dialog.TextEnteredPrimary.Text;
                    newParent = dialog.TextEnteredSecondary.Text;

                    if (result == true)
                    {
                        MessageBoxResult confirmSelection = ConfirmationDialog.Show($"You want update the IP Table on {device.NetworkConfiguration.Hostname}, using the following IP ID:\r\r*{newIPID}*\r\rThis touchpanel will connect to: **{newParent}**\r\r***Is this correct***?", "Confirm IP ID Entry", MessageBoxButton.YesNo);
                        //old version using default messagebox
                        //MessageBoxResult confirmSelection = MessageBox.Show($"You want update the IP Table on {device.NetworkConfiguration.Hostname}, using the following IP ID:\r\r{newIPID}\r\rThis touchpanel will connect to: {newParent}\r\rIs this correct?", "Confirm IP ID Entry", MessageBoxButton.YesNo);
                        if (confirmSelection == MessageBoxResult.Yes) { confirmed = true; }
                    }
                    else
                    {
                        MessageBoxResult cancelOperation = ConfirmationDialog.Show($"Listen here, you *gotta* enter an IP ID if you want me to send it. Do you want to send a new IP Table to {device.NetworkConfiguration.Hostname} or not?", "Cancel IP Table Adjustment", MessageBoxButton.YesNo);
                        //old version using default messagebox
                        //MessageBoxResult cancelOperation = MessageBox.Show($"Listen here, you gotta enter an IP ID if you want me to send it. Do you want to send a new IP Table to {device.NetworkConfiguration.Hostname} or not?", "Cancel IP Table Adjustment", MessageBoxButton.YesNo);
                        if (cancelOperation == MessageBoxResult.No) { canceled = true; }
                    }
                }

                if (canceled) { canceledDevices.Add(device); }

                if (confirmed && newIPID != String.Empty)
                {
                    DeviceDeploymentAction iptableAction = new DeviceDeploymentAction(DeploymentWizardActions.SendConsoleCommands, $"Clear Current IP Table on {device.NetworkConfiguration.Hostname} @ {device.NetworkConfiguration.Hostname}");
                    iptableAction.AssignAction((token) => { return device.SendConsoleCommand($"iptable -c", iptableAction, token); });
                    device.DeploymentActions.Add(iptableAction);

                    DeviceDeploymentAction setIPTableAction = new DeviceDeploymentAction(DeploymentWizardActions.SendConsoleCommands, $"Set IP Table Entry {newIPID} -> {newParent} on {device.NetworkConfiguration.Hostname} @ {device.NetworkConfiguration.Hostname}");
                    setIPTableAction.AssignAction((token) => { return device.SendConsoleCommand($"addm {newIPID} {newParent}", setIPTableAction, token); });
                    device.DeploymentActions.Add(setIPTableAction);
                }
            });

            if (targetDevices.Count == canceledDevices.Count) { canceled = true; }

            return canceled;
        }

        /// <summary>
        /// a sub-wizard for guiding the user through selection of user interface files to upload to selected devices
        /// </summary>
        /// <param name="targetDevices">a list of valid devices to be targeted by this type of operation</param>
        /// <returns>whether or the operation was canceled during the wizard configuration process</returns>
        private static bool UserInterfaceFiles(List<CrestronDevice> targetDevices) 
        {
            bool canceled = false;

            //create a list of valid models from the devices selected
            List<string> targetDeviceModels = targetDevices.Select(device => device.Model).Distinct().ToList();
            //store the canceled operations so that we can remove them aftewards [prevent invalidoperation exception]
            List<string> canceledDevices = new List<string>();
            //loop through each target device to determine what configuration file should be sent
            targetDeviceModels.ForEach(model =>
            {
                bool confirmed = false;
                bool canceled = false;
                string localFilePath = String.Empty;
                //make sure user confirms or cancels the operation
                while (!confirmed && !canceled)
                {
                    //open select file dialog
                    OpenFileDialog dialog = new OpenFileDialog { Title = $"Select VTZ // CH5Z File for {model} Devices", Filter = "VTZ Files (*.vtz)|*.vtz|VTZ Files (*.VTZ)|*.VTZ|CH5Z Files (*.ch5z)|*.ch5z|CH5Z Files (*.CH5Z)|*.CH5Z", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)};
                    bool? result = dialog.ShowDialog();
                    localFilePath = dialog.FileName;

                    if (result == true)
                    {
                        MessageBoxResult confirmSelection = ConfirmationDialog.Show($"You want update VTZ // CH5Z files on {model} devices, using the following file:\r\r*{Path.GetFileName(localFilePath)}*\r\r***Is this the correct file?***", "Confirm VTZ // CH5Z File Selection", MessageBoxButton.YesNo);
                        //old version using default messagebox
                        //MessageBoxResult confirmSelection = MessageBox.Show($"You want update VTZ // CH5Z files on {model} devices, using the following file:\r\r{Path.GetFileName(localFilePath)}\r\rIs this the correct file?", "Confirm VTZ // CH5Z File Selection", MessageBoxButton.YesNo);
                        if (confirmSelection == MessageBoxResult.Yes) { confirmed = true; }
                    }
                    else
                    {
                        MessageBoxResult cancelOperation = ConfirmationDialog.Show($"Listen here, you gotta select a file if you want me to send it. Do you want to send a display file to {model} devices or not?", "Cancel VTZ // CH5Z File Send", MessageBoxButton.YesNo);
                        //old version using default messagebox
                        //MessageBoxResult cancelOperation = MessageBox.Show($"Listen here, you gotta select a file if you want me to send it. Do you want to send a display file to {model} devices or not?", "Cancel VTZ // CH5Z File Send", MessageBoxButton.YesNo);
                        if (cancelOperation == MessageBoxResult.No) { canceled = true; }
                    }
                }

                if (canceled) { canceledDevices.Add(model); }

                if (confirmed)
                {
                    targetDevices.Where(d => d.Model == model).ToList().ForEach(d =>
                    {
                        DeviceDeploymentAction action = new DeviceDeploymentAction(DeploymentWizardActions.SendUserInterfaces, $"Send {Path.GetFileName(localFilePath)} to {d.NetworkConfiguration.Hostname} @ {d.NetworkConfiguration.Hostname}");
                        string remoteFileName = Path.GetFileName(localFilePath);
                        action.AssignAction((token) => { return d.SendFileViaFTP(localFilePath, $"/display/{remoteFileName}", "projectload", action, token); });
                        d.DeploymentActions.Add(action);
                    });
                }
            });
            //loop through each device that matches the current model to add the appropriate deployment action
            canceledDevices.ForEach(model => {
                //remove all devices that match each canceled model
                targetDevices.Where(d => d.Model == model).ToList().ForEach(remove => targetDevices.Remove(remove));
            });

            if (targetDevices.Count == 0) { canceled = true; }

            return canceled;
        }

        /// <summary>
        /// a wizard for guiding the user through the deployment of configuration files to a processors /user/ folder
        /// </summary>
        /// <param name="prompt">whether or not the user should be re-prompted to select a more specific set of devices</param>
        /// <returns>whether or the operation was canceled during the wizard configuration process</returns>
        public static bool ConfigurationFiles(bool prompt)
        {
            bool operationCanceled = false;
            //prompt for target devices [filter by processor for now]
            List<CrestronDevice> targetDevices = DetermineTargetDevices(prompt, DeploymentWizardActions.SendConfigurationFiles, DiscoveredDevices.ProgrammingCapableDevices);
            //store the canceled operations so that we can remove them aftewards [prevent invalidoperation exception]
            List<CrestronDevice> canceledDevices = new List<CrestronDevice>();
            //loop through each target device to determine what configuration file should be sent
            targetDevices.ForEach(d =>
            {
                bool confirmed = false;
                bool canceled = false;
                string localFilePath = String.Empty;
                string remoteFileName = "config.json";
                //make sure user confirms or cancels the operation
                while (!confirmed && !canceled)
                {
                    //open select file dialog
                    OpenFileDialog dialog = new OpenFileDialog { Title = "Select Configuration File", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)};
                    bool? result = dialog.ShowDialog();
                    localFilePath = dialog.FileName;

                    if (result == true) {
                        MessageBoxResult confirmSelection = ConfirmationDialog.Show($"You have selected the following configuration file:\r\r*{Path.GetFileName(localFilePath)}*\r\r***Is this the correct file?***", "Confirm Configuration File Selection", MessageBoxButton.YesNo);
                        //old version using default messagebox
                        //MessageBoxResult confirmSelection = MessageBox.Show($"You have selected the following configuration file:\r\r{Path.GetFileName(localFilePath)}\r\rIs this the correct file?", "Confirm Configuration File Selection", MessageBoxButton.YesNo);
                        
                        if (confirmSelection == MessageBoxResult.Yes) 
                        { 
                            confirmed = true;
                            SimpleTextEntryDialog remoteFileNameDialog = new SimpleTextEntryDialog("Please enter desired filename once the selected file is uploaded to the processor's *user* folder.", "Desired Filename ***(with extension)***", "Configuration Filename Entry", @"^(?!.*[ .]$)[^<>:""/\\|?*\x00-\x1F]+$", mainWindow);
                            bool? fileNameConfirm = remoteFileNameDialog.ShowDialog();
                            if (fileNameConfirm == true) { remoteFileName = remoteFileNameDialog.TextEntered.Text; }
                        }
                    }
                    else {
                        MessageBoxResult cancelOperation = ConfirmationDialog.Show("Listen here, you *gotta* select a file if you want me to send it. Do you want to send a config file to this device or not?", "Cancel Configuration File Send", MessageBoxButton.YesNo);
                        //old version using default messagebox
                        //MessageBoxResult cancelOperation = MessageBox.Show("Listen here, you gotta select a file if you want me to send it. Do you want to send a config file to this device or not?", "Cancel Configuration File Send", MessageBoxButton.YesNo);
                        if (cancelOperation == MessageBoxResult.No) { canceled = true; }
                    }
                }

                if (canceled) { canceledDevices.Add(d); }

                if (confirmed) {
                    DeviceDeploymentAction action = new DeviceDeploymentAction(DeploymentWizardActions.SendConfigurationFiles, $"Send {Path.GetFileName(localFilePath)} to {d.NetworkConfiguration.Hostname} @ {d.NetworkConfiguration.Hostname}");
                    action.AssignAction((token) => { return d.SendFileViaFTP(localFilePath, $"/user/{remoteFileName}", "", action, token); });
                    d.DeploymentActions.Add(action);
                }
            });

            //remove all necessary devices
            canceledDevices.ForEach(device => targetDevices.Remove(device));
            //if the user canceled all of the deployments, then the entire operation should be considered canceled
            if (targetDevices.Count == 0) { operationCanceled = true; }

            return (operationCanceled);
        }

        /// <summary>
        /// a wizard for guiding the user through the deployment of firmware files to a processors /firmware/ folder
        /// </summary>
        /// <param name="prompt">whether or not the user should be re-prompted to select a more specific set of devices</param>
        /// <returns>whether or the operation was canceled during the wizard configuration process</returns>
        public static bool Firmware(bool prompt)
        {
            bool canceled = false;
            //prompt for target devices
            List<CrestronDevice> targetDevices = DetermineTargetDevices(prompt, DeploymentWizardActions.SendFirmware);
            //create a list of valid models from the devices selected
            List<string> targetDeviceModels = targetDevices.Select(device => device.Model).Distinct().ToList();
            //store the canceled operations so that we can remove them aftewards [prevent invalidoperation exception]
            List<string> canceledDevices = new List<string>();
            //loop through each target device to determine what configuration file should be sent
            targetDeviceModels.ForEach(model =>
            {
                bool confirmed = false;
                bool canceled = false;
                string localFilePath = String.Empty;
                //if we decide to 
                while (!confirmed && !canceled)
                {
                    //open select file dialog
                    OpenFileDialog dialog = new OpenFileDialog { Title = $"Select Firmware File for {model} Devices", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)};
                    bool? result = dialog.ShowDialog();
                    localFilePath = dialog.FileName;

                    if (result == true)
                    {
                        MessageBoxResult confirmSelection = ConfirmationDialog.Show($"You want update firmware on {model} devices, using the following firmware file:\r\r*{Path.GetFileName(localFilePath)}*\r\r***Is this the correct file?***", "Confirm Firmware File Selection", MessageBoxButton.YesNo);
                        //old version using default messagebox
                        //MessageBoxResult confirmSelection = MessageBox.Show($"You want update firmware on {model} devices, using the following firmware file:\r\r{Path.GetFileName(localFilePath)}\r\rIs this the correct file?", "Confirm Firmware File Selection", MessageBoxButton.YesNo);
                        if (confirmSelection == MessageBoxResult.Yes) { confirmed = true; }
                    }
                    else
                    {
                        MessageBoxResult cancelOperation = ConfirmationDialog.Show($"Listen here, you gotta select a file if you want me to send it. Do you want to send a firmware file to {model} devices or not?", "Cancel Configuration File Send", MessageBoxButton.YesNo);
                        //old version using default messagebox
                        //MessageBoxResult cancelOperation = MessageBox.Show($"Listen here, you gotta select a file if you want me to send it. Do you want to send a firmware file to {model} devices or not?", "Cancel Configuration File Send", MessageBoxButton.YesNo);
                        if (cancelOperation == MessageBoxResult.No) { canceled = true; }
                    }
                }

                if (canceled) { canceledDevices.Add(model); }
                
                if (confirmed)
                {
                    targetDevices.Where(d => d.Model == model).ToList().ForEach(d =>
                    {
                        DeviceDeploymentAction action = new DeviceDeploymentAction(DeploymentWizardActions.SendFirmware, $"Send {Path.GetFileName(localFilePath)} to {d.NetworkConfiguration.Hostname} @ {d.NetworkConfiguration.Hostname}");
                        string remoteFileName = Path.GetFileName(localFilePath);
                        string command = Path.GetExtension(localFilePath) == ".zip" ? "pushupdate full" : $"puf \\romdisk\\user\\system\\{remoteFileName}";
                        action.AssignAction((token) => { return d.SendFileViaFTP(localFilePath, $"/firmware/{remoteFileName}", command, action, token); });
                        d.DeploymentActions.Add(action);
                    });
                }
            });

            //loop through each device that matches the current model to add the appropriate deployment action
            canceledDevices.ForEach(model => {
                //remove all devices that match each canceled model
                targetDevices.Where(d => d.Model == model).ToList().ForEach(remove => targetDevices.Remove(remove));
            });

            if (targetDevices.Count == 0) { canceled = true; }

            return (canceled);
        }

        private static DeviceNetworkConfiguration GetCurrentNetworkConfiguration(CrestronDevice device, CancellationToken token)
        {
            DeviceNetworkConfiguration config = device.GetCurrentNetworkConfiguration(token);

            string? response = device.SendConsoleCommandWithResponse("dhcp", token);

            if (response != null) { Log.Debug($"{prefix} DHCP Status: {response}"); }

            return config;
        }

        /// <summary>
        /// a wizard for guiding the user through the configuration the network settings on the selected devices
        /// </summary>
        /// <param name="prompt">whether or not the user should be re-promted to select a more specific set of devices</param>
        /// <returns>whether or the operation was canceled during the wizard configuration process</returns>
        public static bool IPConfiguration(bool prompt)
        {
            bool canceled = false;

            //prompt for target devices [filter by touchpanel for now] -- add support for processors in the future
            List<CrestronDevice> targetDevices = DetermineTargetDevices(prompt, DeploymentWizardActions.SetNetworkInformation, DiscoveredDevices.AnyCrestronDevice);
            //create a list of canceled devices
            List<CrestronDevice> canceledDevices = new List<CrestronDevice>();
            //loop through each target device to determine what configuration file should be sent
            targetDevices.ForEach(device =>
            {
                bool confirmed = false;
                bool canceled = false;
                bool enableDHCP = true;
                bool updateIPAddress = false;
                bool updateSubnetMask = false;
                bool updateGateway = false;
                bool updateDNSPrimary = false;
                bool updateDNSSecondary = false;
                bool updateHostname = false;

                string hostname = "";
                string ipAddress = "";
                string subnetMask = "";
                string gateway = "";
                string dnsSecondary = "";
                string dnsPrimary = "";

                DeviceNetworkConfiguration current = GetCurrentNetworkConfiguration(device, new CancellationTokenSource().Token);
                
                //make sure user confirms or cancels the operation
                while (!confirmed && !canceled)
                {
                    //create ip configuration window and wait for result
                    NetworkConfiguration configuration = new NetworkConfiguration($"Please provide the new network configuration you would like to send to {device.NetworkConfiguration.Hostname} @ {device.NetworkConfiguration.Hostname}", $"Update {device.NetworkConfiguration.Hostname} Network Configuration");
                    bool? result = configuration.ShowDialog();

                    if (result == true)
                    {
                        string message = $"Your provided configuration will perform the following actions:\r\r";

                        message += $"*{(configuration.EnableDHCP.IsChecked == true ? "Enable" : "Disable")}* DHCP";

                        if (configuration.EnableDHCP.IsChecked != null) { enableDHCP = (bool)configuration.EnableDHCP.IsChecked; }

                        if (configuration.HostnameEntered.Text != String.Empty) {
                            hostname = configuration.HostnameEntered.Text;
                            message += $"\r\rUpdate Hostname: *{hostname}*";
                            updateHostname = true;
                        }
                        
                        if (configuration.EnableDHCP.IsChecked == false) {
                            if (configuration.IPAddressEntered.Text != String.Empty) {
                                ipAddress = configuration.IPAddressEntered.Text;
                                message += $"\r\rUpdate IP Address: *{ipAddress}*";
                                updateIPAddress = true;
                            }
                            if (configuration.SubnetMaskEntered.Text != String.Empty) {
                                subnetMask = configuration.SubnetMaskEntered.Text;
                                message += $"\r\rUpdate Subnet Mask: *{subnetMask}*";
                                updateSubnetMask = true;
                            }
                            if (configuration.DefaultGatewayEntered.Text != String.Empty) {
                                gateway = configuration.DefaultGatewayEntered.Text;
                                message += $"\r\rUpdate Default Gateway IP Address: *{gateway}*";
                                updateGateway = true;
                            }
                            if (configuration.PrimaryDNSEntered.Text != String.Empty) {
                                dnsPrimary = configuration.PrimaryDNSEntered.Text;
                                message += $"\r\rUpdate Primary DNS Server IP Address: *{dnsPrimary}*";
                                updateDNSPrimary = true;
                            }
                            if (configuration.SecondaryDNSEntered.Text != String.Empty) {
                                dnsSecondary = configuration.SecondaryDNSEntered.Text;
                                message += $"\r\rUpdate Secondary DNS Server IP Address: *{dnsSecondary}*";
                                updateDNSSecondary = true;
                            }

                            message += "\r\r***Does this all look correct?***";
                        }

                        MessageBoxResult confirmSelection = ConfirmationDialog.Show(message, "Confirm IP Configuration", MessageBoxButton.YesNo);
                        //old version using default messagebox
                        //MessageBoxResult confirmSelection = MessageBox.Show(message, "Confirm IP Configuration", MessageBoxButton.YesNo);

                        if (confirmSelection == MessageBoxResult.Yes) { confirmed = true; }
                    }
                    else
                    {
                        MessageBoxResult cancelOperation = ConfirmationDialog.Show($"Listen here, you gotta provide IP configuration details if you want me to adjust things. Do you want to send a new IP configuration to {device.NetworkConfiguration.Hostname} or not?", "Cancel IP Configuration Update", MessageBoxButton.YesNo);
                        //old version using default messagebox
                        //MessageBoxResult cancelOperation = MessageBox.Show($"Listen here, you gotta provide IP configuration details if you want me to adjust things. Do you want to send a new IP configuration to {device.NetworkConfiguration.Hostname} or not?", "Cancel IP Configuration Update", MessageBoxButton.YesNo);
                        if (cancelOperation == MessageBoxResult.No) { canceled = true; }
                    }
                }
                
                if (canceled) { canceledDevices.Add(device); }

                if (confirmed)
                {
                    //force dhcp state DHCP should be enabled
                    DeviceDeploymentAction dhcp = new DeviceDeploymentAction(DeploymentWizardActions.SendConsoleCommands, $"{(enableDHCP == true ? "Enable" : "Disable")} DHCP");
                    dhcp.AssignAction((token) => { return device.SendConsoleCommand($"dhcp 0 {(enableDHCP == true ? "on" : "off")}", dhcp, token); });
                    device.DeploymentActions.Add(dhcp);

                    if (updateHostname)
                    {
                        DeviceDeploymentAction action = new DeviceDeploymentAction(DeploymentWizardActions.SendConsoleCommands, $"Update Hostname: {hostname}");
                        action.AssignAction((token) => { return device.SendConsoleCommand($"hostname {hostname}", action, token); });
                        device.DeploymentActions.Add(action);
                    }

                    //if dhcp is not enabled
                    if (!enableDHCP)
                    {
                        if (updateIPAddress)
                        {
                            DeviceDeploymentAction action = new DeviceDeploymentAction(DeploymentWizardActions.SendConsoleCommands, $"Update IP Address: {ipAddress}");
                            action.AssignAction((token) => { return device.SendConsoleCommand($"ipaddr 0 {ipAddress}", action, token); });
                            device.DeploymentActions.Add(action);
                        }

                        if (updateSubnetMask)
                        {
                            DeviceDeploymentAction action = new DeviceDeploymentAction(DeploymentWizardActions.SendConsoleCommands, $"Update Subnet Mask: {subnetMask}");
                            action.AssignAction((token) => { return device.SendConsoleCommand($"ipmask 0 {subnetMask}", action, token); });
                            device.DeploymentActions.Add(action);
                        }

                        if (updateGateway)
                        {
                            DeviceDeploymentAction action = new DeviceDeploymentAction(DeploymentWizardActions.SendConsoleCommands, $"Update Default Gateway: {gateway}");
                            action.AssignAction((token) => { return device.SendConsoleCommand($"defr 0 {gateway}", action, token); });
                            device.DeploymentActions.Add(action);
                        }

                        if (updateDNSPrimary)
                        {
                            DeviceDeploymentAction action = new DeviceDeploymentAction(DeploymentWizardActions.SendConsoleCommands, $"Update DNS Server 1: {dnsPrimary}");
                            action.AssignAction((token) => { return device.UpdateDnsServer(dnsPrimary, action, token); });
                            device.DeploymentActions.Add(action);
                        }

                        if (updateDNSSecondary)
                        {
                            DeviceDeploymentAction action = new DeviceDeploymentAction(DeploymentWizardActions.SendConsoleCommands, $"Update DNS Server 2: {dnsSecondary}");
                            action.AssignAction((token) => { return device.UpdateDnsServer(dnsPrimary, action, token); });
                            device.DeploymentActions.Add(action);
                        }
                    }

                    DeviceDeploymentAction reboot = new DeviceDeploymentAction(DeploymentWizardActions.SendConsoleCommands, $"Reboot Device");
                    reboot.AssignAction((token) => { return device.SendConsoleCommand("reboot", reboot, token); });
                    device.DeploymentActions.Add(reboot);
                }
            });

            if (targetDevices.Count == canceledDevices.Count) { canceled = true; }

            return canceled;
        }
    
        /// <summary>
        /// a wizard for guiding the user through provisioning a new device, assigning the administrator username and password
        /// </summary>
        /// <param name="prompt"></param>
        /// <returns></returns>
        public static bool DeviceProvisioning(bool prompt)
        {
            bool canceled = false;
            bool confirmed = false;

            //default credentials, might need to be updated at some point
            string user = "admin";
            string pass = "CCS$erv!ce";
            //string pass = "Av!dex$erv!ce";

            //prompt for target devices [filter by touchpanel for now] -- add support for processors in the future
            List<CrestronDevice> targetDevices = DetermineTargetDevices(prompt, DeploymentWizardActions.ProvisionNewDevice, DiscoveredDevices.AnyCrestronDevice);
            //create a list of canceled devices
            List<CrestronDevice> canceledDevices = new List<CrestronDevice>();

            MessageBoxResult batch = MessageBoxResult.None;

            while (batch == MessageBoxResult.None) {
                batch = ConfirmationDialog.Show("Would you like to provide a single set of administrator credentials *for all devices* selected to be provisioned?", "Provision Method", MessageBoxButton.YesNoCancel);
                
                if (batch == MessageBoxResult.None) { batch = MessageBoxResult.Cancel; }
                else if (batch == MessageBoxResult.Yes)
                {
                    //make sure user confirms or cancels the operation
                    while (!confirmed && !canceled)
                    {
                        //prompt the user to confirm the normal default credentials
                        if (canceled == false)
                        {
                            string message = $"We will use the following credentials to create an *administrator* account on the all devices selected for provisioning.\r\rUsername: **{user}**\r\rPassword: **{pass}**\r\r***Is this correct?***";

                            MessageBoxResult confirmSelection = ConfirmationDialog.Show(message, "Confirm New Credentials", MessageBoxButton.YesNo);
                            //old version using default messagebox
                            //MessageBoxResult confirmSelection = MessageBox.Show(message, "Confirm New Credentials", MessageBoxButton.YesNo);

                            if (confirmSelection == MessageBoxResult.Yes) { confirmed = true; }
                            else
                            {
                                //create credential window and wait for result
                                if (mainWindow != null) { (user, pass, canceled) = ConnectionDetails(mainWindow, $"Please provide credentials to be assigned to the **administrator** account."); }
                            }
                        }
                    }
                }
            }
            
            //if the user canceled the entire operation set the flag
            if (batch == MessageBoxResult.Cancel) { canceled = true; }
            else { confirmed = true; }

            //if the user decided to not cancel the entire operation
            if (!canceled)
            {
                //loop through each target device to determine what configuration file should be sent
                targetDevices.ForEach(device =>
                {
                    if (batch == MessageBoxResult.No)
                    {
                        confirmed = false;
                        canceled = false;
                        //make sure user confirms or cancels the operation
                        while (!confirmed && !canceled)
                        {
                            //prompt the user to confirm the normal default credentials
                            if (canceled == false)
                            {
                                string message = $"We will use the following credentials to create an *administrator* account on the following device:\r\r*{device.NetworkConfiguration.Hostname}* @ **{device.NetworkConfiguration.IPAddress}**:\r\rUsername: **{user}**\r\rPassword: **{pass}**\r\r***Is this correct?***";

                                MessageBoxResult confirmSelection = ConfirmationDialog.Show(message, "Confirm New Credentials", MessageBoxButton.YesNo);
                                //old version using default messagebox
                                //MessageBoxResult confirmSelection = MessageBox.Show(message, "Confirm New Credentials", MessageBoxButton.YesNo);

                                if (confirmSelection == MessageBoxResult.Yes) { confirmed = true; }
                                else
                                {
                                    //create credential window and wait for result
                                    if (mainWindow != null) { (user, pass, canceled) = ConnectionDetails(mainWindow, $"Please provide credentials to be assigned to the **administrator account** on\r\r*{device.NetworkConfiguration.Hostname}* @ **{device.NetworkConfiguration.IPAddress}**"); }
                                }
                            }
                        }
                    }


                    if (canceled) { canceledDevices.Add(device); }

                    if (confirmed)
                    {
                        DeviceDeploymentAction action = new DeviceDeploymentAction(DeploymentWizardActions.ProvisionNewDevice, $"Provision {device.NetworkConfiguration.Hostname} @ {device.NetworkConfiguration.IPAddress} with new credentials | Username: {user} Password: {pass}");
                        action.AssignAction((token) => { return device.ProvisionNewDevice(user, pass, action, token); });
                        device.DeploymentActions.Add(action);
                    }
                });

                if (targetDevices.Count == canceledDevices.Count) { canceled = true; }
            }

            return canceled;
        }
        
        /// <summary>
        /// the main deployment wizard function
        /// </summary>
        /// <param name="username">ssh username</param>
        /// <param name="password">ssh password</param>
        /// <param name="parent">parent window caller</param>
        public static (string?, string?) ConfigureDeployment(string? username, string? password, MainWindow parent)
        {
            List<DeploymentWizardAction> selectedActions = DeploymentWizardActions.Options.Where(i => i.IsSelected).ToList();
            bool cancel = false;

            //assign only the parent and let the user determine if they need to change the default crestron credentials after being prompted later
            if (selectedActions.Count == 1 && selectedActions.Any(item => item.Name == DeploymentWizardActions.ProvisionNewDevice)) { 
                DeploymentWizard.mainWindow = parent;
                username = Constants.DefaultUsername; 
                password = Constants.DefaultPassword;
            }
            //if we have selected more than one action, even if some of those actions are provisioning devices
            else
            {
                //if both of these are null, this is the first run
                if (password == null && password == null) {

                    MessageBoxResult result = ConfirmationDialog.Show("Would you like to use the *standard* credentials, or enter ***custom*** credentials to deploy to these devices?", "Use Default Credentials?", MessageBoxButton.YesNo);
                    //old version using default messagebox
                    //MessageBoxResult result = MessageBox.Show("Would you like to use the standard credentials, or enter custom credentials to deploy to these devices?", "Use Default Credentials?", MessageBoxButton.YesNo);
                    //prompt the user for custom credentials
                    if (result == MessageBoxResult.No) { (username, password, cancel) = DeploymentWizard.ConnectionDetails(parent); }
                    //assign the default credentials and bypass credential entry
                    else { username = Constants.DefaultUsername; password = Constants.DefaultPassword; }
                }
                //if this is not the first run, provide a quick confirmation to re-use the existing credentials
                else
                {
                    MessageBoxResult result = ConfirmationDialog.Show($"Do you want to re-use the credentials you provided *previously*?\r\rUsername: *{username}*\r\rPassword:***{password}***", "Use Existing Credentials", MessageBoxButton.YesNo);
                    //old version using default messagebox
                    //MessageBoxResult result = MessageBox.Show($"Do you want to re-use the credentials you provided previously?\r\rUsername: {username}\r\rPassword:{password}", "Use Existing Credentials", MessageBoxButton.YesNo);
                    if (result == MessageBoxResult.No) { (username, password, cancel) = DeploymentWizard.ConnectionDetails(parent); }
                }
            }

            //if the user cancels the operation, exit the wizard
            if (cancel) { return (null, null); }

            //determine if we should provide a re-selection dialog for each of the deployment actions (users may want to only deploy to certain devices
            bool reselectTargetDevices = selectedActions.Count > 1 && DiscoveredDevices.SelectedTargetDevices.Count > 1;

            //run through the wizard for each deployment action selected
            selectedActions.ForEach(action =>
            {
                bool canceled = false;

                switch (action.Name)
                {
                    case (DeploymentWizardActions.ProvisionNewDevice):
                        canceled = DeploymentWizard.DeviceProvisioning(reselectTargetDevices);
                        Log.Debug($"{prefix} Device Provisioning {(canceled == true ? "Canceled" : "Ready")}");
                        break;
                    case (DeploymentWizardActions.SendConsoleCommands):
                        canceled = DeploymentWizard.ConsoleCommands(reselectTargetDevices);
                        Log.Debug($"{prefix} Console Command Deployment {(canceled == true ? "Canceled" : "Ready")}");
                        break;
                    case (DeploymentWizardActions.SendConfigurationFiles):
                        canceled = DeploymentWizard.ConfigurationFiles(reselectTargetDevices);
                        Log.Debug($"{prefix} Configuration File Deployment {(canceled == true ? "Canceled" : "Ready")}");
                        break;
                    case (DeploymentWizardActions.SendUserInterfaces):
                        canceled = DeploymentWizard.UserInterfaces(reselectTargetDevices);
                        Log.Debug($"{prefix} User Interface Deployment {(canceled == true ? "Canceled" : "Ready")}");
                        break;
                    case (DeploymentWizardActions.SendProgramming):
                        canceled = DeploymentWizard.Programming(reselectTargetDevices);
                        Log.Debug($"{prefix} Programming Deployment {(canceled == true ? "Canceled" : "Ready")}");
                        break;
                    case (DeploymentWizardActions.SendFirmware):
                        canceled = DeploymentWizard.Firmware(reselectTargetDevices);
                        Log.Debug($"{prefix} Firmware Deployment {(canceled == true ? "Canceled" : "Ready")}");
                        break;
                    case (DeploymentWizardActions.SetNetworkInformation):
                        canceled = DeploymentWizard.IPConfiguration(reselectTargetDevices);
                        Log.Debug($"{prefix} IP Configuration Update {(canceled == true ? "Canceled" : "Ready")}");
                        break;
                }
            });

            return (username, password);
        }
    }
}
