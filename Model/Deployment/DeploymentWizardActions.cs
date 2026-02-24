using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace CrestronDeploymentTool.Model.Deployment
{
    /// <summary>
    /// a static class to hold available deployment wizard actions to be provided to the end user
    /// </summary>
    public static class DeploymentWizardActions
    {
        public static readonly ObservableCollection<DeploymentWizardAction> Options = new ObservableCollection<DeploymentWizardAction>();

        public const string SendProgramming = "Send/Update Programming";
        public const string SendUserInterfaces = "Send/Update User Interfaces";
        public const string SendFirmware = "Update Firmware";
        public const string SendConfigurationFiles = "Send/Update Configuration Files";
        public const string SendConsoleCommands = "Send Console Commands";
        public const string ProvisionNewDevice = "Provision New Device";
        public const string SetNetworkInformation = "Set/Update IP Configuration";
        public const string UpdateIPTable = "Send/Update Device IP Table";

        static DeploymentWizardActions()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject())) { LoadElements(); }
            else { LoadElements(); }
        }

        /// <summary>
        /// loads the available wizards that have been configured
        /// </summary>
        private static void LoadElements()
        {
            new List<(string, string)>() {
                (ProvisionNewDevice, "select this to configure the default credentials on a OOTB multiple device"),
                (SetNetworkInformation, "select this to adjust network settings on multiple devices"),
                (SendFirmware, "select this to update firmware on multiple devices"),
                (SendConfigurationFiles, "select this to send configuration files to multiple devices"),
                (SendProgramming, "select this to update programming to multiple control processors"),
                (UpdateIPTable, "select this to update the ip table on multiple devices"),
                (SendUserInterfaces, "select this to update user interfaces on multiple devices"),
                (SendConsoleCommands, "select this to send a console command to multiple devices"),
            }.ForEach(item => { Options.Add(new DeploymentWizardAction(false, item.Item1, item.Item2)); });
        }

        /// <summary>
        /// checkes if an action already exists in the list to prevent duplicates
        /// </summary>
        /// <param name="action">the action to add</param>
        /// <returns>a bool representing whether or not the action was actually added to the list</returns>
        public static bool Add(DeploymentWizardAction action)
        {
            bool result = false;

            if (!Options.ToList().Any(i => i.Name == action.Name))
            {
                result = true;
                Options.Add(action);
            }

            return result;
        }
    }
}
