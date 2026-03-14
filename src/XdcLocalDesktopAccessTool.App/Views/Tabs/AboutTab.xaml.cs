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
            var win = new SupportWindow
            {
                Owner = Window.GetWindow(this)
            };
            win.ShowDialog();
        }
    }
}
