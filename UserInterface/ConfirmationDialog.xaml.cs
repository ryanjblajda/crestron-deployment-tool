using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Serilog;

namespace CrestronDeploymentTool.UserInterface
{
    /// <summary>
    /// Interaction logic for ConfirmationDialog.xaml
    /// </summary>
    public partial class ConfirmationDialog : Window
    {
        private const string prefix = "ConfirmationDialog | ";

        private MessageBoxButton? buttonConfiguration;
        public MessageBoxResult Result { get; private set; }

        public ConfirmationDialog()
        {
            this.Owner = Application.Current.MainWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            InitializeComponent();
        }

        public ConfirmationDialog(string prompt, string title, MessageBoxButton? button) : this()
        {
            Utilities.TextHelpers.ParseFormattedText(prompt, this.Message);
            this.Title = title;
            this.buttonConfiguration = button;
            DetermineButtonText();
        }

        public static MessageBoxResult Show(string prompt, string title, MessageBoxButton? buttons = null)
        {
            if (buttons == null) { buttons = MessageBoxButton.OK; }
            ConfirmationDialog dialog = new ConfirmationDialog(prompt, title, buttons);
            dialog.ShowDialog();
            return dialog.Result;
        }

        private void DetermineButtonText()
        {
            switch (this.buttonConfiguration)
            {
                case MessageBoxButton.YesNo:
                    //ButtonLeft.Visibility = Visibility.Collapsed;
                    ButtonCenter.Visibility = Visibility.Collapsed;
                    //ButtonRight.Visibility = Visibility.Collapsed;

                    ButtonLeft.Content = "Yes";
                    ButtonCenter.Content = "Collapsed";
                    ButtonRight.Content = "No";

                    break;
                case MessageBoxButton.YesNoCancel:
                    //ButtonLeft.Visibility = Visibility.Collapsed;
                    //ButtonCenter.Visibility = Visibility.Collapsed;
                    //ButtonRight.Visibility = Visibility.Collapsed;

                    ButtonLeft.Content = "Yes";
                    ButtonCenter.Content = "No";
                    ButtonRight.Content = "Cancel";

                    break;
                case MessageBoxButton.OK:
                    ButtonLeft.Visibility = Visibility.Collapsed;
                    ButtonCenter.Visibility = Visibility.Collapsed;
                    //ButtonRight.Visibility = Visibility.Collapsed;
                    
                    ButtonLeft.Content = "Collapsed";
                    ButtonCenter.Content = "Collapsed";
                    ButtonRight.Content = "Ok";

                    break;
                case MessageBoxButton.OKCancel:
                    //ButtonLeft.Visibility = Visibility.Collapsed;
                    ButtonCenter.Visibility = Visibility.Collapsed;
                    //ButtonRight.Visibility = Visibility.Collapsed;

                    ButtonLeft.Content = "Ok";
                    ButtonCenter.Content = "Collapsed";
                    ButtonRight.Content = "Cancel";

                    break;
            }
        }

        private void OnButtonPressed(object sender, RoutedEventArgs e)
        {
            Button clicked = (Button)sender;

            switch (this.buttonConfiguration)
            {
                case MessageBoxButton.YesNo:
                    if (sender == ButtonLeft) { this.Result = MessageBoxResult.Yes; }
                    else if (sender == ButtonCenter) { this.Result = MessageBoxResult.None; }
                    else if (sender == ButtonRight) { this.Result = MessageBoxResult.No; }
                    else { Log.Warning($"{prefix} Invalid Button Clicked!"); }

                    break;
                case MessageBoxButton.YesNoCancel:
                    if (sender == ButtonLeft) { this.Result = MessageBoxResult.Yes; }
                    else if (sender == ButtonCenter) { this.Result = MessageBoxResult.No; }
                    else if (sender == ButtonRight) { this.Result = MessageBoxResult.Cancel; }
                    else { Log.Warning($"{prefix} Invalid Button Clicked!"); }

                    break;
                case MessageBoxButton.OK:
                    if (sender == ButtonLeft) { this.Result = MessageBoxResult.None; }
                    else if (sender == ButtonCenter) { this.Result = MessageBoxResult.None; }
                    else if (sender == ButtonRight) { this.Result = MessageBoxResult.OK; }
                    else { Log.Warning($"{prefix} Invalid Button Clicked!"); }

                    break;
                case MessageBoxButton.OKCancel:
                    if (sender == ButtonLeft) { this.Result = MessageBoxResult.OK; }
                    else if (sender == ButtonCenter) { this.Result = MessageBoxResult.None; }
                    else if (sender == ButtonRight) { this.Result = MessageBoxResult.Cancel; }
                    else { Log.Warning($"{prefix} Invalid Button Clicked!"); }

                    break;
            }

            this.DialogResult = true;
            this.Close();
        }
    }
}
