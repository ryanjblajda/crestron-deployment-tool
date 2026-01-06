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
        public ConnectionCredentialsDialog(Window owner)
        {
            this.Owner = owner;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.Title = "Provide SSH Credentials";
            InitializeComponent();
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
