using System;
using System.Windows;
using System.Windows.Controls;
using XdcLocalDesktopAccessTool.App.Views.Controls;

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
                var main = Window.GetWindow(this) as MainWindow;
                if (main == null)
                    return;

                main.ShowCreateWalletOverlay();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to open Create Wallet overlay.\n\n" + ex,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void GenerateKeystoreButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var main = Window.GetWindow(this) as MainWindow;
                if (main == null)
                    return;

                main.ShowGenerateKeystoreOverlay();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to open Generate Keystore overlay.\n\n" + ex,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

    }
}

