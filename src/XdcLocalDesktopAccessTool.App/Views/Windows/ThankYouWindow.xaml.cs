using System.Windows;

namespace XdcLocalDesktopAccessTool.App.Views.Windows
{
    public partial class ThankYouWindow : Window
    {
        public ThankYouWindow()
        {
            InitializeComponent();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}