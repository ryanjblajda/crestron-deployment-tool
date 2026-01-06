using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrestronDeploymentTool.Model.TargetDevices.DeviceDeployment
{
    /// <summary>
    /// an enum to represent how the application should connect to the crestron device
    /// </summary>
    internal enum ConnectionResult
    {
        ConnectionFailure,
        UseSsh,
        UseTelnet        
    }
}
