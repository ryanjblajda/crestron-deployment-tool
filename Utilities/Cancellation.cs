using CrestronDeploymentTool.Model.TargetDevices.DeviceDeployment;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrestronDeploymentTool.Utilities
{
    /// <summary>
    /// a static helper class for dealing with cancellation tokens
    /// </summary>
    internal static class Cancellation
    {
        private const string prefix = "Cancellation |";

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

                Debug.WriteLine($"{prefix} Cancellation Requested");

                if (action != null)
                {
                    Debug.WriteLine($"{prefix} Update Deployment Action");
                    action.Status = DeviceDeploymentActionStatus.CompleteCanceled;
                    action.Message += "\r!! Operation Canceled !!";
                }
            }

            return exit;
        }
    }
}
