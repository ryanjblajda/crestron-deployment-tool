using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CrestronDeploymentTool.Model.Deployment
{
    /// <summary>
    /// a class to represent the deployment status of the application
    /// </summary>
    public class DeploymentStatus : INotifyPropertyChanged
    {
        private bool active;
        private bool idle;
        public bool Running
        {
            get => active;
            set
            {
                active = value;
                Idle = !value;
                OnPropertyChanged(nameof(Running));
            }
        }
        public bool Idle
        {
            get => idle;
            set 
            { 
                idle = value;
                OnPropertyChanged(nameof(Idle));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
