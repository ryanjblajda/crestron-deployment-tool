using CrestronDeploymentTool.Utilities;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CrestronDeploymentTool.UserInterface
{
    /// <summary>
    /// Interaction logic for AddCrestronDevice.xaml
    /// </summary>
    public partial class ConnectionCredentialsDialog : Window
    {
        public ConnectionCredentialsDialog(Window owner, string? prompt = null)
        {
            this.Owner = owner;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            InitializeComponent();
            if (prompt == null) { prompt = $"Please provide the username and password used to connect to **already provisioned devices**"; }
            TextHelpers.ParseFormattedText(prompt , this.Prompt);
        }

        private void CredentialsValid()
        {
            if (this.Username.Text != String.Empty)
            {
                this.DialogResult = true;
                this.Close();
            }
        }

        private void OnCredentialsConfirmedClicked(object sender, RoutedEventArgs e)
        {
            CredentialsValid();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { CredentialsValid(); }
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            this.ConfirmCredentialsButton.IsEnabled = ((TextBox)sender).Text != String.Empty;
        }
    }
}
