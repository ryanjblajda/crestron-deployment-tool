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
        private const string listDnsResponsePattern = @"(?<device>[\w]*[\s]*[\d]+)[\s|]*(?<address>\d+\.\d+\.\d+\.\d+)[\s|]*(?<type>[\w]+)[\s|]+";
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
   }
}
