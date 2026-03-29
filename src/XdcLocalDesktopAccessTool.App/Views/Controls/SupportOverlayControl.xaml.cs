using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace XdcLocalDesktopAccessTool.App.Views.Controls
{
    public partial class SupportOverlayControl : UserControl
    {
        public SupportOverlayControl()
        {
            InitializeComponent();
            LoadLogo();
        }

        private void LoadLogo()
        {
            // 1) Prefer cropped embedded resource
            try
            {
                var packUri = new Uri("pack://application:,,,/Assets/Images/XDC_Background_Cropped.png", UriKind.Absolute);

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
                // continue to fallback
            }

            // 2) Fall back to original embedded resource
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
                // continue to disk fallback
            }

            // 3) Fall back to cropped disk file
            try
            {
                var filePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Images", "XDC_Background_Cropped.png");
                if (File.Exists(filePath))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(filePath, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();

                    LogoImage.Source = bmp;
                    LogoImage.Visibility = Visibility.Visible;
                    return;
                }
            }
            catch
            {
                // continue to final fallback
            }

            // 4) Final fallback to original disk file
            try
            {
                var filePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Images", "XDC_Background.png");
                if (File.Exists(filePath))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(filePath, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();

                    LogoImage.Source = bmp;
                    LogoImage.Visibility = Visibility.Visible;
                    return;
                }
            }
            catch
            {
                // ignore
            }

            LogoImage.Visibility = Visibility.Collapsed;
        }

        private async void CopyAddress_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(DonationAddressTextBox.Text ?? string.Empty);

            var originalText = CopyAddressButton.Content?.ToString() ?? "Copy address";

            CopyAddressButton.Content = "Copied";
            CopyAddressButton.IsEnabled = false;

            await Task.Delay(1000);

            CopyAddressButton.Content = originalText;
            CopyAddressButton.IsEnabled = true;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow main)
            {
                main.CloseSupportOverlay();
            }
        }
    }
}
