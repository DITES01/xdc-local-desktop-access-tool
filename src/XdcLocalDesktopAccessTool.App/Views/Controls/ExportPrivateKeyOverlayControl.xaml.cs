using System;
using System.Windows;
using System.Windows.Controls;

namespace XdcLocalDesktopAccessTool.App.Views.Controls
{
    public partial class ExportPrivateKeyOverlayControl : UserControl
    {
        private string _addressXdc = string.Empty;
        private string _privateKeyHex0x = string.Empty;

        public ExportPrivateKeyOverlayControl()
        {
            InitializeComponent();
        }

        public void SetPrivateKeyContext(string addressXdc, string privateKeyHex0x)
        {
            _addressXdc = addressXdc ?? string.Empty;
            _privateKeyHex0x = privateKeyHex0x ?? string.Empty;

            if (AddressTextBox != null)
                AddressTextBox.Text = _addressXdc;

            if (PrivateKeyPasswordBox != null)
                PrivateKeyPasswordBox.Password = _privateKeyHex0x;

            if (PrivateKeyTextBox != null)
                PrivateKeyTextBox.Text = _privateKeyHex0x;
        }

        private void ShowPrivateKeyCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (PrivateKeyPasswordBox == null || PrivateKeyTextBox == null)
                return;

            PrivateKeyTextBox.Text = _privateKeyHex0x;
            PrivateKeyTextBox.Visibility = Visibility.Visible;
            PrivateKeyPasswordBox.Visibility = Visibility.Collapsed;
        }

        private void ShowPrivateKeyCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (PrivateKeyPasswordBox == null || PrivateKeyTextBox == null)
                return;

            PrivateKeyPasswordBox.Password = _privateKeyHex0x;
            PrivateKeyPasswordBox.Visibility = Visibility.Visible;
            PrivateKeyTextBox.Visibility = Visibility.Collapsed;
        }

        private void CopyPrivateKeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_privateKeyHex0x))
                return;

            try
            {
                Clipboard.SetText(_privateKeyHex0x);

                if (MainCardBorder != null)
                    MainCardBorder.IsEnabled = false;

                if (CopyResultPanel != null)
                    CopyResultPanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to copy private key.\n\n" + ex.Message,
                    "Copy private key",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void CopyResultOkButton_Click(object sender, RoutedEventArgs e)
        {
            if (CopyResultPanel != null)
                CopyResultPanel.Visibility = Visibility.Collapsed;

            if (MainCardBorder != null)
                MainCardBorder.IsEnabled = true;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            var main = Window.GetWindow(this) as MainWindow;
            if (main != null)
                main.CloseModalOverlay();
        }
    }
}
