using System;
using System.Windows;
using System.Windows.Controls;
using XdcLocalDesktopAccessTool.App.Views.Windows;

namespace XdcLocalDesktopAccessTool.App.Views.Tabs
{
    public partial class AdvancedTab : UserControl
    {
        public AdvancedTab()
        {
            InitializeComponent();
        }

        private void CreateWalletButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new CreateWalletWindow();
                window.Owner = Window.GetWindow(this);
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to open Create Wallet window.\n\n" + ex,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void GenerateKeystoreButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new GenerateKeystoreFileWindow();
                window.Owner = Window.GetWindow(this);
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to open Generate Keystore window.\n\n" + ex,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
