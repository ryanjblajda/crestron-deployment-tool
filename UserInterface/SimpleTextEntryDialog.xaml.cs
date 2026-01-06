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
    public partial class SimpleTextEntryDialog : Window
    {
        private Regex _validator;

        public SimpleTextEntryDialog(string message, string messageprompt, string title, string entryvalidator, Window? parent)
        {
            InitializeComponent();

            if (parent != null)
            {
                this.Owner = parent;
                this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            TextHelpers.ParseFormattedText(messageprompt, this.MessagePrompt);
            TextHelpers.ParseFormattedText(message, this.Message);

            this.Title = title;
            this._validator = new Regex(entryvalidator);
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
            if (_validator.IsMatch(this.TextEntered.Text)) { this.TextEntered.Background = new SolidColorBrush(Color.FromArgb(51, 00, 00, 00)); }
            else { this.TextEntered.Background = new SolidColorBrush(Colors.Red); }

            this.ConfirmButton.IsEnabled = _validator.IsMatch(this.TextEntered.Text);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && this.ConfirmButton.IsEnabled) { TextEntryConfirmed(); }
        }
    }
}
