using CrestronDeploymentTool.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CrestronDeploymentTool.UserInterface
{
    /// <summary>
    /// Interaction logic for ProgrammingOptions.xaml
    /// </summary>
    public partial class ProgrammingOptions : Window
    {
        public ProgrammingOptions(string program, string device)
        {
            InitializeComponent();
            this.ProgramSlot.ItemsSource = new object[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            this.ProgramSlot.SelectedItem = this.ProgramSlot.Items[0];
            TextHelpers.ParseFormattedText($"You have decided to send **{program}** to *{device}*.\r\r" +
                $"Please select from the options below to determine how the program will be sent.\r\r" +
                $"This application *currently* has no way of knowing if {device} is capable of running multiple program slots. Please make sure you select the correct program slot.", this.Message);
        }

        private void OnConfirmOptionsClicked(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
