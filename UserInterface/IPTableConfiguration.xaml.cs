using CrestronDeploymentTool.Model.TargetDevices;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Media;

namespace CrestronDeploymentTool.UserInterface
{
    /// <summary>
    /// Interaction logic for AddCrestronDevice.xaml
    /// </summary>
    public partial class IPTableConfiguration : Window
    {
        private List<IPTableEntry> Entries = new List<IPTableEntry>();

        public IPTableConfiguration(List<IPTableEntry> ipTable)
        { 
            this.Owner = Application.Current.MainWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.Title = "Add Known Crestron Device";
            this.Entries = ipTable;
            InitializeComponent();

            if (DesignerProperties.GetIsInDesignMode(this)) { LoadElements(); }
        }

        private void LoadElements()
        {
            this.Entries.Add(new IPTableEntry("127.0.0.1", 30, "Gway", 41794));
        }
    }
}
