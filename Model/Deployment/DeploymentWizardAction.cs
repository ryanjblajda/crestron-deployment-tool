using Serilog;

namespace CrestronDeploymentTool.Model.Deployment
{
    /// <summary>
    /// an action taken by a device as a result of completing the configuration of a deployment wizard
    /// </summary>
    public class DeploymentWizardAction
    {
        private const string prefix = "DeploymentWizardAction |";
        public bool IsSelected { get; set; }
        public string Name { get; private set; }
        public string Description { get; private set; }

        public DeploymentWizardAction(bool isChecked, string name, string description)
        {
            Log.Debug($"{prefix} Creating New Action: {name} -> {description}");
            IsSelected = isChecked;
            Name = name;
            Description = description;
        }
    }
}
