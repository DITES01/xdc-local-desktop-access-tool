using System.Windows;
using System.Windows.Controls;

namespace XdcLocalDesktopAccessTool.App.Views.Controls
{
    public partial class ThankYouOverlayControl : UserControl
    {
        public ThankYouOverlayControl()
        {
            InitializeComponent();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            var main = Window.GetWindow(this) as MainWindow;
            main?.CloseModalOverlay();
        }
    }
}
