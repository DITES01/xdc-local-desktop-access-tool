using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XdcLocalDesktopAccessTool.App.Views.Controls;

namespace XdcLocalDesktopAccessTool.App
{
    public partial class MainWindow : Window
    {
        private TabItem? _pendingTabChangeTarget;
        private bool _suppressTabSelectionChanged;
        private TabItem? _lastSelectedTab;
        private AuthorisationOverlayControl? _activeOverlay;
        private Views.Controls.GenerateKeystoreOverlayControl? _activeKeystoreOverlay;
        private Views.Controls.CreateWalletOverlayControl? _activeCreateWalletOverlay;
        private Views.Controls.ExportPrivateKeyOverlayControl? _activeExportPrivateKeyOverlay;
        private UserControl? _previousModalOverlayContent;

        private const double DefaultWindowWidth = 1080;
        private const double DefaultWindowHeight = 940;

        public MainWindow()
        {
            InitializeComponent();

            Loaded += (_, __) =>
            {
                _lastSelectedTab = MainTabs.SelectedItem as TabItem;
            };
        }

        public void ShowSupportOverlay()
        {
            MainTabs.IsEnabled = false;

            ModalOverlayPresenter.Content = null;
            ModalOverlayPresenter.Visibility = Visibility.Collapsed;

            SupportOverlay.Visibility = Visibility.Visible;
            OverlayHost.Visibility = Visibility.Visible;
        }

        public void CloseSupportOverlay()
        {
            SupportOverlay.Visibility = Visibility.Collapsed;

            if (ModalOverlayPresenter.Visibility != Visibility.Visible)
            {
                OverlayHost.Visibility = Visibility.Collapsed;
                MainTabs.IsEnabled = true;
            }
        }

        public void ShowModalOverlay(UserControl overlayContent)
        {
            if (overlayContent == null)
                return;

            if (ModalOverlayPresenter.Visibility == Visibility.Visible &&
                ModalOverlayPresenter.Content is UserControl currentOverlay &&
                !ReferenceEquals(currentOverlay, overlayContent))
            {
                _previousModalOverlayContent = currentOverlay;
            }
            else if (ModalOverlayPresenter.Visibility != Visibility.Visible)
            {
                _previousModalOverlayContent = null;
            }

            MainTabs.IsEnabled = false;
            SupportOverlay.Visibility = Visibility.Collapsed;

            ModalOverlayPresenter.Content = overlayContent;
            ModalOverlayPresenter.Visibility = Visibility.Visible;
            OverlayHost.Visibility = Visibility.Visible;

            overlayContent.Focus();
            Keyboard.Focus(overlayContent);
        }

        public void CloseModalOverlay()
        {
            if (_previousModalOverlayContent != null)
            {
                var previousOverlay = _previousModalOverlayContent;
                _previousModalOverlayContent = null;

                ModalOverlayPresenter.Content = previousOverlay;
                ModalOverlayPresenter.Visibility = Visibility.Visible;
                OverlayHost.Visibility = Visibility.Visible;
                MainTabs.IsEnabled = false;

                if (previousOverlay is AuthorisationOverlayControl previousAuthOverlay)
                {
                    _activeOverlay = previousAuthOverlay;
                }

                previousOverlay.Focus();
                Keyboard.Focus(previousOverlay);
                return;
            }

            bool? overlayResult = _activeOverlay?.Result;

            ModalOverlayPresenter.Content = null;
            ModalOverlayPresenter.Visibility = Visibility.Collapsed;
            _activeOverlay = null;

            if (SupportOverlay.Visibility != Visibility.Visible)
            {
                OverlayHost.Visibility = Visibility.Collapsed;
                MainTabs.IsEnabled = true;
            }

            if (overlayResult == true && SendTabControl != null)
            {
                SendTabControl.RefreshAuthUi();
                SendTabControl.AppendLine("Authorised.");
            }
        }

        public bool ShowAuthorisationOverlay()
        {
            var overlay = new AuthorisationOverlayControl();
            _activeOverlay = overlay;
            ShowModalOverlay(overlay);
            return false;
        }

        public void ShowGenerateKeystoreOverlay(
            string? addressXdc = null,
            string? address0x = null,
            string? privateKeyHex0x = null,
            string? derivationPathUsed = null,
            bool usedBip39Passphrase = false,
            string? mnemonicPhrase = null,
            bool mnemonicUsesBip39Passphrase = false)
        {
            var overlay = new Views.Controls.GenerateKeystoreOverlayControl();
            _activeKeystoreOverlay = overlay;

            if (!string.IsNullOrWhiteSpace(privateKeyHex0x))
            {
                overlay.SetWalletContext(
                    addressXdc ?? string.Empty,
                    address0x ?? string.Empty,
                    privateKeyHex0x ?? string.Empty,
                    derivationPathUsed ?? string.Empty,
                    usedBip39Passphrase);
            }

            if (!string.IsNullOrWhiteSpace(mnemonicPhrase))
            {
                overlay.SetRecoveryPhraseContext(
                    mnemonicPhrase ?? string.Empty,
                    mnemonicUsesBip39Passphrase);
            }

            ShowModalOverlay(overlay);
        }

        public void CloseGenerateKeystoreOverlay()
        {
            _activeKeystoreOverlay = null;
            CloseModalOverlay();
        }

        public void ShowCreateWalletOverlay()
        {
            var overlay = new Views.Controls.CreateWalletOverlayControl();
            _activeCreateWalletOverlay = overlay;
            ShowModalOverlay(overlay);
        }

        public void CloseCreateWalletOverlay()
        {
            _activeCreateWalletOverlay = null;
            CloseModalOverlay();
        }

        public void ShowExportPrivateKeyOverlay(string addressXdc, string privateKeyHex0x)
        {
            var overlay = new Views.Controls.ExportPrivateKeyOverlayControl();
            _activeExportPrivateKeyOverlay = overlay;
            overlay.SetPrivateKeyContext(addressXdc ?? string.Empty, privateKeyHex0x ?? string.Empty);
            ShowModalOverlay(overlay);
        }

        public void CloseExportPrivateKeyOverlay()
        {
            _activeExportPrivateKeyOverlay = null;
            CloseModalOverlay();
        }

        private bool ConfirmAndHandleLeavingTab(TabItem? tabBeingLeft)
        {
            if (tabBeingLeft == null)
                return true;

            if (tabBeingLeft == SendTabItem)
            {
                if (SendTabControl != null)
                {
                    if (SendTabControl.HasDataToLoseForTabChange())
                    {
                        var result = MessageBox.Show(
                            "Changing tabs will deauthorise the current wallet and clear the current Send tab information.\n\n" +
                            "This includes the authorised session, entered address, amount, gas settings, and any preview/output shown in this tab.\n\n" +
                            "You will need to authorise again to sign transactions.\n\n" +
                            "Do you want to continue?",
                            "Confirm Tab Change",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (result != MessageBoxResult.Yes)
                            return false;

                        SendTabControl.DeauthoriseAndClearForTabChange();
                    }
                    else
                    {
                        SendTabControl.ClearBalanceEntryFields();
                    }
                }

                return true;
            }

            

            return true;
        }

        private void TabItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not TabItem clickedTab)
                return;

            if (MainTabs.SelectedItem is not TabItem currentTab)
                return;

            if (clickedTab == currentTab)
                return;

            _pendingTabChangeTarget = clickedTab;

            if (!ConfirmAndHandleLeavingTab(currentTab))
            {
                _pendingTabChangeTarget = null;
                e.Handled = true;
                return;
            }

            _suppressTabSelectionChanged = true;
            MainTabs.SelectedItem = clickedTab;
            _suppressTabSelectionChanged = false;

            _lastSelectedTab = clickedTab;
            _pendingTabChangeTarget = null;
            e.Handled = true;
        }

        private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressTabSelectionChanged)
                return;

            if (e.Source != MainTabs)
                return;

            if (MainTabs.SelectedItem is not TabItem selectedTab)
                return;

            if (_lastSelectedTab == null)
            {
                _lastSelectedTab = selectedTab;
                return;
            }

            if (selectedTab == _lastSelectedTab)
                return;

            if (_pendingTabChangeTarget != null)
            {
                _lastSelectedTab = selectedTab;
                _pendingTabChangeTarget = null;
                return;
            }

            if (!ConfirmAndHandleLeavingTab(_lastSelectedTab))
            {
                _suppressTabSelectionChanged = true;
                MainTabs.SelectedItem = _lastSelectedTab;
                _suppressTabSelectionChanged = false;
                return;
            }

            _lastSelectedTab = selectedTab;
        }
    }
}








