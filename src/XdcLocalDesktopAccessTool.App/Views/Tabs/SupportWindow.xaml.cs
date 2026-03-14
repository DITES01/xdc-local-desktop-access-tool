using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace XdcLocalDesktopAccessTool.App.Views.Tabs
{
    public partial class SupportWindow : Window
    {
        public SupportWindow()
        {
            InitializeComponent();
            LoadLogo();
        }

        private void LoadLogo()
        {
            // Prefer embedded WPF Resource (pack URI)
            try
            {
                var packUri = new Uri("pack://application:,,,/Assets/Images/XDC_Background.png", UriKind.Absolute);

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = packUri;
                bmp.EndInit();
                bmp.Freeze();

                LogoImage.Source = bmp;
                LogoImage.Visibility = Visibility.Visible;
                return;
            }
            catch
            {
                // fallback to disk
            }

            try
            {
                var filePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Images", "XDC_Background.png");
                if (!File.Exists(filePath))
                {
                    LogoImage.Visibility = Visibility.Collapsed;
                    return;
                }

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(filePath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();

                LogoImage.Source = bmp;
                LogoImage.Visibility = Visibility.Visible;
            }
            catch
            {
                LogoImage.Visibility = Visibility.Collapsed;
            }
        }

        private async void CopyAddress_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(DonationAddressTextBox.Text?.Trim() ?? "");

                if (sender is System.Windows.Controls.Button btn)
                {
                    var originalContent = btn.Content;
                    btn.Content = "Copied";
                    btn.IsEnabled = false;

                    await System.Threading.Tasks.Task.Delay(1000);

                    btn.Content = originalContent;
                    btn.IsEnabled = true;
                }
            }
            catch
            {
                MessageBox.Show("Could not copy address.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}