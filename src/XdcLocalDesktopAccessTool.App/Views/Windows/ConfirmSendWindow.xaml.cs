using System.Windows;

namespace XdcLocalDesktopAccessTool.App.Views.Windows
{
    public partial class ConfirmSendWindow : Window
    {
        public ConfirmSendWindow(string details)
        {
            InitializeComponent();
            DetailsTextBox.Text = details ?? "";
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
