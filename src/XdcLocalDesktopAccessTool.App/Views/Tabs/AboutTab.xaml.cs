using System.Windows;
using System.Windows.Controls;

namespace XdcLocalDesktopAccessTool.App.Views.Tabs
{
    public partial class AboutTab : UserControl
    {
        public AboutTab()
        {
            InitializeComponent();
        }

        private void SupportThisTool_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow main)
            {
                main.ShowSupportOverlay();
            }
        }
    }
}