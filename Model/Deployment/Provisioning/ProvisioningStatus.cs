using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrestronDeploymentTool.Model.Deployment.ProvisioningState
{
    internal enum ProvisioningStatus
    {
        WaitingUsername,
        WaitingPassword,
        WaitingVerification,
        WaitingComplete,
        Success,
        Failure,
        Error,
        NotStarted
    }
}
