using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrestronDeploymentTool.Model.TargetDevices.DeviceDeployment
{
    /// <summary>
    /// an enum representing the status a deployment action can be in
    /// </summary>
    public enum DeviceDeploymentActionStatus
    {
        NotStarted,
        Waiting,
        InProgress,
        SSHFailed,
        SSHSuccess,
        TelnetFailed,
        TelnetSuccess,
        SendingCommand,
        SendingCommandFailed,
        SendingCommandSuccess,
        SendingFile,
        SendingFileFailed,
        SendingFileSuccess,
        WaitingForResponse,
        CompleteFailure,
        CompleteSuccess,
        CompleteCanceled
    }
}
