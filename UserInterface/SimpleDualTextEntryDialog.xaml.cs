using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CrestronDeploymentTool.Utilities;

namespace CrestronDeploymentTool.UserInterface
{
    /// <summary>
    /// Interaction logic for ConsoleCommand.xaml
    /// </summary>
    public partial class SimpleDualTextEntryDialog : Window
    {
        private Regex _textEntryPrimaryValidator;
        private Regex _textEntrySecondaryValidator;
        private bool _secondaryValid;
        private bool _primaryValid;

        public SimpleDualTextEntryDialog(string message, string messageprompt, string messagepromptsecondary, string title, string textEntryPrimaryValidator, string textEntrySecondaryValidator, Window? parent)
        {
            InitializeComponent();

            if (parent != null)
            {
                this.Owner = parent;
                this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            TextHelpers.ParseFormattedText(messageprompt, this.MessagePromptPrimary);
            TextHelpers.ParseFormattedText(messagepromptsecondary, this.MessagePromptSecondary);
            TextHelpers.ParseFormattedText(message, this.Message);

            this.Title = title;
            this._textEntryPrimaryValidator = new Regex(textEntryPrimaryValidator);
            this._textEntrySecondaryValidator = new Regex(textEntrySecondaryValidator);

            this.ConfirmButton.IsEnabled = false;
        }

        private void TextEntryConfirmed()
        {
            this.DialogResult = true;
            this.Close();
        }

        private void OnTextEntryConfirmed(object sender, RoutedEventArgs e)
        {
            TextEntryConfirmed();
        }

        private void OnTextEntryChanged(object sender, TextChangedEventArgs e)
        {
            if (_textEntryPrimaryValidator.IsMatch(this.TextEnteredPrimary.Text) == false) 
            { 
                ((TextBox)sender).Background = new SolidColorBrush(Colors.Red);

                if (((TextBox)sender) == this.TextEnteredPrimary) { this._primaryValid = false; }
                else { this._secondaryValid = false; }
                
                this.ConfirmButton.IsEnabled = this._primaryValid && this._secondaryValid;
            }
            else {
                ((TextBox)sender).Background = new SolidColorBrush(Color.FromArgb(51, 00, 00, 00));

                if (((TextBox)sender) == this.TextEnteredPrimary) { this._primaryValid = true; }
                else { this._secondaryValid = true; }
                
                this.ConfirmButton.IsEnabled = this._primaryValid && this._secondaryValid;
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && this.ConfirmButton.IsEnabled) { TextEntryConfirmed(); }
        }
    }
}
