using CrestronDeploymentTool.UserInterface;
using CrestronDeploymentTool.Model.TargetDevices;
using System.Linq;
using System.Windows;
using System.Collections.ObjectModel;
using System.Collections.Immutable;
using CrestronDeploymentTool.Model.Deployment.ConsoleCommand;
using CrestronDeploymentTool.Model.TargetDevices.DeviceDeployment;
using Microsoft.Win32;
using System.IO;
using System.IO.Compression;

namespace CrestronDeploymentTool.Model.Deployment
{
    public static class DeploymentWizard
    {
        private static Window? mainWindow;
        public static (string, string, bool) ConnectionDetails(Window owner)
        {
            mainWindow = owner;

            string username = String.Empty, password = String.Empty;

            bool? result = false;
            bool cancelDeployment = false;

            while (result == false)
            {
                ConnectionCredentialsDialog dialog = new ConnectionCredentialsDialog(owner);
                result = dialog.ShowDialog();

                if (result == true)
                {
                    username = dialog.Username.Text;
                    password = dialog.Password.Password;
                }
                else 
                { 
                    MessageBoxResult why = MessageBox.Show("You must enter credentials....how else can I connect to the devices?", "Provide Credentials (Pretty Please)", MessageBoxButton.OKCancel); 
                    
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
                    if (DiscoveredDevices.SelectedTargetDevices.Any(d => d.Name == item))
                    {
                        actionTargetDevices.Add(DiscoveredDevices.SelectedTargetDevices.First(d => d.Name == item));
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
            if (filter != null) { filtered = DiscoveredDevices.SelectedTargetDevices.Where(d => filter.Any(prefix => d.Model.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))).ToList().Select(d => d.Name).ToList(); }
            //if not, get em all
            else { filtered = DiscoveredDevices.SelectedTargetDevices.Select(i => i.Name).ToList(); }

            //if we need to prompt for selection we provide the list with this specific selection
            if (prompt) {
                //we only want to show the prompt, if there is a device to select
                if (filtered.Count > 0) { devices = PromptForTargetDevices(action, filtered); }
                else { MessageBox.Show($"There are no devices available from your initial selection that support {action}."); }
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
            ConsoleCommandSendType result = ConsoleCommandSendType.Batch;



            return result;
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
                        MessageBoxResult cancel = MessageBox.Show("Look man, if you wanted to send console commands you can't provide me an empty string. Either provide me a command or cancel the operation.", "Console Command Cannot Be Empty!", MessageBoxButton.OKCancel);
                        
                        if (cancel == MessageBoxResult.Cancel) { return(canceled = true); }
                    }

                    command = dialog.TextEntered.Text;
                }

                if (result == true) 
                { 
                    targets.ForEach(d => 
                    {
                        //create a new action
                        DeviceDeploymentAction action = new DeviceDeploymentAction(DeploymentWizardActions.SendConsoleCommands, $"Perform {command} command on {d.Name} @ {d.IpAddress}");
                        //assign a lamba function wrapping the send console command call, passing in the command, the device we need to perform the action on, and a reference to the action so that it can be updated with status
                        action.AssignAction((token) => { return DeviceDeploymentAction.SendConsoleCommand(command, d, action, token); });
                        //assign that action to the device for later use
                        d.DeploymentActions.Add(action);
                    }); 
                }
            }
            else if (type == ConsoleCommandSendType.Unique) 
            {

            }

            //confirm -> loop back through if needed

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
            //prompt for target devices [filter by touchpanel for now] -- add support for processors in the future
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
                    OpenFileDialog dialog = new OpenFileDialog { Title = "Select Program File", Filter = crestronCompiledFilter, Multiselect = false };
                    bool? result = dialog.ShowDialog();
                    localFilePath = dialog.FileName;

                    if (result == true)
                    {
                        MessageBoxResult confirmSelection = MessageBox.Show($"You have selected the following program:\r\r{Path.GetFileName(localFilePath)}", "Confirm Program File Selection", MessageBoxButton.YesNo);
                        if (confirmSelection == MessageBoxResult.Yes) 
                        { 
                            confirmed = true;

                            bool? programOptionsConfirmed = false;
                            
                            while(programOptionsConfirmed == false) 
                            {
                                ProgrammingOptions progOptionsDialog = new ProgrammingOptions(Path.GetFileName(localFilePath), device.Name);
                                bool? exited = progOptionsDialog.ShowDialog();
                                programSlot = (int)progOptionsDialog.ProgramSlot.SelectedItem;
                                includeSIGFile = progOptionsDialog.IncludeSIGFile.IsChecked;
                                overwriteIPTable = progOptionsDialog.OverwriteIPTable.IsChecked;

                                if (exited == false) { MessageBoxResult cancelOperation = MessageBox.Show($"Listen here, you gotta select program options, since you decided to send {Path.GetFileName(localFilePath)} to {device.Name}.", "Program Options Required", MessageBoxButton.OK); }
                                else {
                                    string programOptionMessage = $"You have selected the following options for the deployment of: {Path.GetFileName(localFilePath)} to {device.Name}\r\r" +
                                    $"Program Slot: {programSlot}\r" +
                                    $"Include Sig File: {(progOptionsDialog.IncludeSIGFile.IsChecked == true ? "Yes" : "No")}\r" +
                                    $"Overwrite IP Table: {(progOptionsDialog.OverwriteIPTable.IsChecked == true ? "Yes" : "No")}\r\r" +
                                    $"Are these options correct?";
                                    MessageBoxResult optionsConfirmedResponse = MessageBox.Show(programOptionMessage, "Confirm Program Options", MessageBoxButton.YesNo);
                                    if (optionsConfirmedResponse == MessageBoxResult.Yes) { programOptionsConfirmed = true; }
                                }
                            }
                        }
                    }
                    else
                    {
                        MessageBoxResult cancelOperation = MessageBox.Show($"Listen here, you gotta select a program if you want me to send it. Do you want to send a new program to {device.Name} or not?", "Cancel Program Update", MessageBoxButton.YesNo);
                        if (cancelOperation == MessageBoxResult.No) { canceled = true; }
                    }
                }

                if (canceled) { canceledDevices.Add(device); }

                if (confirmed)
                {
                    DeviceDeploymentAction stopProgram = new DeviceDeploymentAction(DeploymentWizardActions.SendConsoleCommands, "Stop Running Program");
                    stopProgram.AssignAction((token) => { return DeviceDeploymentAction.SendConsoleCommand("stopprog -p:1", device, stopProgram, token); });
                    device.DeploymentActions.Add(stopProgram);

                    string message = $"Upload {Path.GetFileName(localFilePath)} to slot {programSlot}, {(overwriteIPTable == true ? "updating IP table" : "")} {(includeSIGFile == true ? "sending sig file" : "")}";
                    string postUploadCommand = $"progload -p:{programSlot}";

                    //make sure to add the no flag if we have not elected to overwrite the ip table (or somehow the value is null, because that should be the default operation)
                    if (overwriteIPTable == false || overwriteIPTable == null) { postUploadCommand += "-n";  }

                    DeviceDeploymentAction action = new DeviceDeploymentAction(DeploymentWizardActions.SendProgramming, message);
                    
                    action.AssignAction((token) => { return DeviceDeploymentAction.SendFileViaFTP(localFilePath, $"/program{programSlot:D2}/{Path.GetFileName(localFilePath)}", postUploadCommand, device, action, token); });
                    device.DeploymentActions.Add(action);

                    //if we need to upload the sig file, we will need to add a deployment action
                    if (includeSIGFile == true) {
                        MemoryStream? zigArchive = CreateZIGFile(localFilePath);
                        
                        if (zigArchive != null) {
                            DeviceDeploymentAction sigAction = new DeviceDeploymentAction("Send Sig File", $"Upload Sig File for {Path.GetFileName(localFilePath)}");
                            sigAction.AssignAction((token) => { return DeviceDeploymentAction.SendFileViaFTP(zigArchive, $"/program{programSlot:D2}/{Path.GetFileNameWithoutExtension(localFilePath)}.zig", "", device, sigAction, token); });
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
                MessageBoxResult result = MessageBox.Show("Do you want to update/configure IP Table entries?", "Update IP Table Entries", MessageBoxButton.YesNo);
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
                    SimpleDualTextEntryDialog dialog = new SimpleDualTextEntryDialog($"Please provide the new IP ID for *{device.Name}* in *HEXADECIMAL*", "IP ID Entry", "Processor IP Address/Hostname", "Updated IP ID", @"^(?:0[1-9A-Fa-f]|[1-9A-Fa-f][0-9A-Fa-f])$", validNewParentPattern, mainWindow);
                    bool? result = dialog.ShowDialog();
                    newIPID = dialog.TextEnteredPrimary.Text;
                    newParent = dialog.TextEnteredSecondary.Text;

                    if (result == true)
                    {
                        MessageBoxResult confirmSelection = MessageBox.Show($"You want update the IP Table on {device.Name}, using the following IP ID:\r\r{newIPID}\r\rThis touchpanel will connect to: {newParent}\r\rIs this correct?", "Confirm IP ID Entry", MessageBoxButton.YesNo);
                        if (confirmSelection == MessageBoxResult.Yes) { confirmed = true; }
                    }
                    else
                    {
                        MessageBoxResult cancelOperation = MessageBox.Show($"Listen here, you gotta enter an IP ID if you want me to send it. Do you want to send a new IP Table to {device.Name} or not?", "Cancel IP Table Adjustment", MessageBoxButton.YesNo);
                        if (cancelOperation == MessageBoxResult.No) { canceled = true; }
                    }
                }

                if (canceled) { canceledDevices.Add(device); }

                if (confirmed && newIPID != String.Empty)
                {
                    DeviceDeploymentAction action = new DeviceDeploymentAction(DeploymentWizardActions.SendConsoleCommands, $"Clear Current IP Table on {device.Name} @ {device.IpAddress}");
                    action.AssignAction((token) => { return DeviceDeploymentAction.SendConsoleCommand($"iptable -c", device, action, token); });
                    device.DeploymentActions.Add(action);

                    action = new DeviceDeploymentAction(DeploymentWizardActions.SendConsoleCommands, $"Set IP Table Entry {newIPID} -> {newParent} on {device.Name} @ {device.IpAddress}");
                    action.AssignAction((token) => { return DeviceDeploymentAction.SendConsoleCommand($"addm {newIPID} {newParent}", device, action, token); });
                    device.DeploymentActions.Add(action);
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
                    OpenFileDialog dialog = new OpenFileDialog { Title = $"Select VTZ // CH5Z File for {model} Devices", Filter = "VTZ Files (*.vtz)|*.vtz|VTZ Files (*.VTZ)|*.VTZ|CH5Z Files (*.ch5z)|*.ch5z|CH5Z Files (*.CH5Z)|*.CH5Z", Multiselect = false };
                    bool? result = dialog.ShowDialog();
                    localFilePath = dialog.FileName;

                    if (result == true)
                    {
                        MessageBoxResult confirmSelection = MessageBox.Show($"You want update VTZ // CH5Z files on {model} devices, using the following file:\r\r{Path.GetFileName(localFilePath)}\r\rIs this the correct file?", "Confirm VTZ // CH5Z File Selection", MessageBoxButton.YesNo);
                        if (confirmSelection == MessageBoxResult.Yes) { confirmed = true; }
                    }
                    else
                    {
                        MessageBoxResult cancelOperation = MessageBox.Show($"Listen here, you gotta select a file if you want me to send it. Do you want to send a display file to {model} devices or not?", "Cancel VTZ // CH5Z File Send", MessageBoxButton.YesNo);
                        if (cancelOperation == MessageBoxResult.No) { canceled = true; }
                    }
                }

                if (canceled) { canceledDevices.Add(model); }

                if (confirmed)
                {
                    targetDevices.Where(d => d.Model == model).ToList().ForEach(d =>
                    {
                        DeviceDeploymentAction action = new DeviceDeploymentAction(DeploymentWizardActions.SendUserInterfaces, $"Send {Path.GetFileName(localFilePath)} to {d.Name} @ {d.IpAddress}");
                        string remoteFileName = Path.GetFileName(localFilePath);
                        action.AssignAction((token) => { return DeviceDeploymentAction.SendFileViaFTP(localFilePath, $"/display/{remoteFileName}", "projectload", d, action, token); });
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
                    OpenFileDialog dialog = new OpenFileDialog { Title = "Select Configuration File", Multiselect = false };
                    bool? result = dialog.ShowDialog();
                    localFilePath = dialog.FileName;

                    if (result == true) {
                        MessageBoxResult confirmSelection = MessageBox.Show($"You have selected the following configuration file:\r\r{Path.GetFileName(localFilePath)}\r\rIs this the correct file?", "Confirm Configuration File Selection", MessageBoxButton.YesNo);
                        
                        if (confirmSelection == MessageBoxResult.Yes) 
                        { 
                            confirmed = true;
                            SimpleTextEntryDialog remoteFileNameDialog = new SimpleTextEntryDialog("Please enter desired filename once the selected file is uploaded to the processor's *user* folder.", "Desired Filename ***(with extension)***", "Configuration Filename Entry", @"^(?!.*[ .]$)[^<>:""/\\|?*\x00-\x1F]+$", mainWindow);
                            bool? fileNameConfirm = remoteFileNameDialog.ShowDialog();
                            if (fileNameConfirm == true) { remoteFileName = remoteFileNameDialog.TextEntered.Text; }
                        }
                    }
                    else {
                        MessageBoxResult cancelOperation = MessageBox.Show("Listen here, you gotta select a file if you want me to send it. Do you want to send a config file to this device or not?", "Cancel Configuration File Send", MessageBoxButton.YesNo);
                        if (cancelOperation == MessageBoxResult.No) { canceled = true; }
                    }
                }

                if (canceled) { canceledDevices.Add(d); }

                if (confirmed) {
                    DeviceDeploymentAction action = new DeviceDeploymentAction(DeploymentWizardActions.SendConfigurationFiles, $"Send {Path.GetFileName(localFilePath)} to {d.Name} @ {d.IpAddress}");
                    action.AssignAction((token) => { return DeviceDeploymentAction.SendFileViaFTP(localFilePath, $"/user/{remoteFileName}", "", d, action, token); });
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
                    OpenFileDialog dialog = new OpenFileDialog { Title = $"Select Firmware File for {model} Devices", Multiselect = false };
                    bool? result = dialog.ShowDialog();
                    localFilePath = dialog.FileName;

                    if (result == true)
                    {
                        MessageBoxResult confirmSelection = MessageBox.Show($"You want update firmware on {model} devices, using the following firmware file:\r\r{Path.GetFileName(localFilePath)}\r\rIs this the correct file?", "Confirm Firmware File Selection", MessageBoxButton.YesNo);
                        if (confirmSelection == MessageBoxResult.Yes) { confirmed = true; }
                    }
                    else
                    {
                        MessageBoxResult cancelOperation = MessageBox.Show($"Listen here, you gotta select a file if you want me to send it. Do you want to send a firmware file to {model} devices or not?", "Cancel Configuration File Send", MessageBoxButton.YesNo);
                        if (cancelOperation == MessageBoxResult.No) { canceled = true; }
                    }
                }

                if (canceled) { canceledDevices.Add(model); }
                
                if (confirmed)
                {
                    targetDevices.Where(d => d.Model == model).ToList().ForEach(d =>
                    {
                        DeviceDeploymentAction action = new DeviceDeploymentAction(DeploymentWizardActions.SendFirmware, $"Send {Path.GetFileName(localFilePath)} to {d.Name} @ {d.IpAddress}");
                        string remoteFileName = Path.GetFileName(localFilePath);
                        string command = Path.GetExtension(localFilePath) == ".zip" ? "pushupdate full" : $"puf \\romdisk\\user\\system\\{remoteFileName}";
                        action.AssignAction((token) => { return DeviceDeploymentAction.SendFileViaFTP(localFilePath, $"/firmware/{remoteFileName}", command, d, action, token); });
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

        /// <summary>
        /// a wizard for guiding the user through the configuration the network settings on the selected devices
        /// </summary>
        /// <param name="prompt">whether or not the user should be re-promted to select a more specific set of devices</param>
        /// <returns>whether or the operation was canceled during the wizard configuration process</returns>
        public static bool IPConfiguration(bool prompt)
        {
            bool canceled = false;
            return canceled;
        }
    }
}
