using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrestronDeploymentTool.Model.Deployment.ConsoleCommand
{
    /// <summary>
    /// an enum to represent the way a user can select to send console commands
    /// </summary>
    internal enum ConsoleCommandSendType
    {
        Batch = 0,
        Unique = 1,
    }
}
