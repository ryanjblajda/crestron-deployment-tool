using CrestronDeploymentTool.Model.TargetDevices.DeviceDeployment;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace CrestronDeploymentTool.Utilities
{
    /// <summary>
    /// a static helper class for dealing with cancellation tokens
    /// </summary>
    internal static class Cancellation
    {
        private const string prefix = "Cancellation |";
        private const string message = "\r!! Operation Canceled !!";

        /// <summary>
        /// checkes the cancellation status of a token and updates the correct deployment action as needed
        /// </summary>
        /// <param name="token">the token to check</param>
        /// <param name="action">the action to update</param>
        /// <returns></returns>
        internal static bool CheckTokenStatus(CancellationToken token, DeviceDeploymentAction? action = null)
        {
            bool exit = false;

            if (token.IsCancellationRequested)
            {
                exit = true;

                Log.Debug($"{prefix} Cancellation Requested");

                if (action != null)
                {
                    Log.Debug($"{prefix} Update Deployment Action");
                    action.Status = DeviceDeploymentActionStatus.CompleteCanceled;
                    if (!action.Message.Contains(message)) { action.Message += message; }
                }
            }

            return exit;
        }
    }
}
