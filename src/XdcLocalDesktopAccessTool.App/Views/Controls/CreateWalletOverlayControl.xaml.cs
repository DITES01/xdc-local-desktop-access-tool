using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using XdcLocalDesktopAccessTool.App.Services;

namespace XdcLocalDesktopAccessTool.App.Views.Controls
{
    public partial class CreateWalletOverlayControl : UserControl
    {
        private bool _isLoaded = false;
        private bool _suppressUiEvents = false;

        private int _lastPhraseLengthIndex = 0;
        private int _lastDerivationPathIndex = 0;

        private string _generatedAddress = string.Empty;     // xdc-prefixed (display/copy)
        private string _generatedAddress0x = string.Empty;   // 0x-prefixed (session/signing)
        private string _generatedPrivateKey = string.Empty;  // 0x-prefixed
        private List<string> _mnemonicWords = new();

        // Current passphrase (kept in-memory only)
        private string _bip39Passphrase = string.Empty;
        private string _confirmBip39Passphrase = string.Empty;

        public CreateWalletOverlayControl()
        {
            InitializeComponent();

            // Make sure defaults are set at launch
            if (DerivationPathComboBox.SelectedIndex < 0) DerivationPathComboBox.SelectedIndex = 0;
            if (PhraseLengthComboBox.SelectedIndex < 0) PhraseLengthComboBox.SelectedIndex = 0;

            ClearGeneratedWalletState(resetLayoutOnly: true);

            // Lock mnemonic layout to current phrase length
            MnemonicGridControl.SetWordCount(GetSelectedWordCount());

            _lastPhraseLengthIndex = PhraseLengthComboBox.SelectedIndex;
            _lastDerivationPathIndex = DerivationPathComboBox.SelectedIndex;

            // BIP39 UI starts hidden and disabled
            Bip39RowGrid.Visibility = Visibility.Hidden;
            ShowBip39PassphraseCheckBox.IsEnabled = false;
            ShowBip39PassphraseCheckBox.IsChecked = false;
            UseBip39PassphraseCheckBox.IsChecked = false;

            
            _isLoaded = true;
        }

        private void CreateNewWalletButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var wordCount = GetSelectedWordCount();
                var derivationPath = GetSelectedDerivationPath();

                var usePassphrase = UseBip39PassphraseCheckBox.IsChecked == true;
                var passphrase = usePassphrase ? (_bip39Passphrase ?? string.Empty) : string.Empty;

                if (usePassphrase && string.IsNullOrWhiteSpace(passphrase))
                {
                    MessageBox.Show(
                        "You have enabled BIP39 passphrase, but the passphrase field is empty.\n\n" +
                        "Either enter a passphrase, or untick 'Use BIP39 passphrase (advanced)'.",
                        "Passphrase required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                if (usePassphrase && string.IsNullOrWhiteSpace(_confirmBip39Passphrase))
                {
                    MessageBox.Show(
                        "Please confirm the BIP39 passphrase.",
                        "Confirm passphrase required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                if (usePassphrase && !string.Equals(passphrase, _confirmBip39Passphrase, StringComparison.Ordinal))
                {
                    MessageBox.Show(
                        "The BIP39 passphrases do not match.\n\nPlease re-enter them carefully.",
                        "Passphrase mismatch",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                var result = WalletDerivationService.GenerateNewWallet(
                    wordCount,
                    passphrase,
                    derivationPath);

                _mnemonicWords = result.MnemonicWords.ToList();
                _generatedPrivateKey = result.PrivateKeyHex0x;
                _generatedAddress0x = result.Address0x;
                _generatedAddress = result.AddressXdc;

                OutputTextBlock.Text =
                    $"Address (xdc): {_generatedAddress}\n" +
                    $"Address (0x): {_generatedAddress0x}\n" +
                    $"Derivation path: {result.DerivationPathUsed}\n" +
                    $"BIP39 passphrase: {(usePassphrase ? "Used" : "Not used")}";

                if (ShowRecoveryPhraseCheckBox.IsChecked == true)
                    MnemonicGridControl.Populate(_mnemonicWords);
                else
                    MnemonicGridControl.Mask(_mnemonicWords.Count);

                ShowRecoveryPhraseCheckBox.IsEnabled = true;
                CopyAddressButton.IsEnabled = true;
                CopyRecoveryPhraseButton.IsEnabled = true;
                GenerateKeystoreButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to generate wallet.\n\n" + ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                ClearGeneratedWalletState(resetLayoutOnly: false);
            }
        }

        private void GenerateKeystoreButton_Click(object sender, RoutedEventArgs e)
{
    try
    {
        if (string.IsNullOrWhiteSpace(_generatedPrivateKey))
        {
            MessageBox.Show(
                "Please create a wallet first.",
                "No wallet",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            "Before continuing, make sure you have written down your seed phrase and any BIP39 passphrase exactly as shown.\n\nIf you lose them, you may not be able to recover this wallet.\n\nDo you want to continue?",
            "Continue to Generate Keystore?",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        var main = Window.GetWindow(this) as MainWindow;
        if (main == null)
            return;

        var mnemonic = string.Join(" ", _mnemonicWords);

        main.ShowGenerateKeystoreOverlay(
            _generatedAddress,
            _generatedAddress0x,
            _generatedPrivateKey,
            GetSelectedDerivationPath(),
            !string.IsNullOrWhiteSpace(_bip39Passphrase),
            mnemonic,
            !string.IsNullOrWhiteSpace(_bip39Passphrase));
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            "Failed to open Generate Keystore overlay.\n\n" + ex.Message,
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
private void CopyAddressButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_generatedAddress))
            {
                MessageBox.Show(
                    "No address available to copy.",
                    "Nothing to Copy",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            Clipboard.SetText(_generatedAddress);

            MessageBox.Show(
                "Address copied to clipboard.",
                "Copied",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void CopyRecoveryPhraseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mnemonicWords == null || _mnemonicWords.Count == 0)
            {
                MessageBox.Show(
                    "No seed phrase available to copy.",
                    "Nothing to Copy",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            Clipboard.SetText(string.Join(" ", _mnemonicWords));

            MessageBox.Show(
                "Seed phrase copied to clipboard.",
                "Copied",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ShowRecoveryPhraseCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            if (_mnemonicWords.Count > 0)
                MnemonicGridControl.Populate(_mnemonicWords);
        }

        private void ShowRecoveryPhraseCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            if (_mnemonicWords.Count > 0)
                MnemonicGridControl.Mask(_mnemonicWords.Count);
            else
                MnemonicGridControl.Clear();
        }

        private void PhraseLengthComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || _suppressUiEvents) return;

            if (HasGeneratedWallet())
            {
                var confirm = MessageBox.Show(
                    "Changing phrase length will clear the current generated wallet.\n\n" +
                    "Make sure you have written down your seed phrase.\n\nContinue?",
                    "Confirm",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes)
                {
                    _suppressUiEvents = true;
                    PhraseLengthComboBox.SelectedIndex = _lastPhraseLengthIndex;
                    _suppressUiEvents = false;
                    return;
                }

                ClearGeneratedWalletState(resetLayoutOnly: false);
            }

            MnemonicGridControl.SetWordCount(GetSelectedWordCount());
            _lastPhraseLengthIndex = PhraseLengthComboBox.SelectedIndex;
        }

        private void DerivationPathComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || _suppressUiEvents) return;

            if (HasGeneratedWallet())
            {
                var confirm = MessageBox.Show(
                    "Changing derivation path will clear the current generated wallet.\n\n" +
                    "Make sure you have written down your seed phrase.\n\nContinue?",
                    "Confirm",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes)
                {
                    _suppressUiEvents = true;
                    DerivationPathComboBox.SelectedIndex = _lastDerivationPathIndex;
                    _suppressUiEvents = false;
                    return;
                }

                ClearGeneratedWalletState(resetLayoutOnly: false);
            }

            _lastDerivationPathIndex = DerivationPathComboBox.SelectedIndex;
        }

        private void UseBip39PassphraseCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            Bip39RowGrid.Visibility = Visibility.Visible;
            ShowBip39PassphraseCheckBox.IsEnabled = true;

            ShowBip39PassphraseCheckBox.IsChecked = false;
            SwapPassphraseVisibility(showPlainText: false);

            MessageBox.Show(
                "The BIP39 passphrase acts as a second password.\n\n" +
                "If a different passphrase is entered, a completely different wallet will be generated.\n\n" +
                "Make sure you record the exact passphrase safely. If it is lost or entered differently, the wallet cannot be recovered correctly.",
                "BIP39 Passphrase",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void UseBip39PassphraseCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            Bip39RowGrid.Visibility = Visibility.Hidden;

            _bip39Passphrase = string.Empty;
            _confirmBip39Passphrase = string.Empty;

            ShowBip39PassphraseCheckBox.IsChecked = false;
            ShowBip39PassphraseCheckBox.IsEnabled = false;

            Bip39PassphrasePasswordBox.Password = string.Empty;
            Bip39PassphraseTextBox.Text = string.Empty;
            ConfirmBip39PassphrasePasswordBox.Password = string.Empty;
            ConfirmBip39PassphraseTextBox.Text = string.Empty;

            SwapPassphraseVisibility(showPlainText: false);
        }

        private void ShowBip39PassphraseCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            SwapPassphraseVisibility(showPlainText: true);
        }

        private void ShowBip39PassphraseCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            SwapPassphraseVisibility(showPlainText: false);
        }

        private void SwapPassphraseVisibility(bool showPlainText)
        {
            if (showPlainText)
            {
                Bip39PassphraseTextBox.Text = Bip39PassphrasePasswordBox.Password;
                ConfirmBip39PassphraseTextBox.Text = ConfirmBip39PassphrasePasswordBox.Password;

                Bip39PassphraseTextBox.Visibility = Visibility.Visible;
                ConfirmBip39PassphraseTextBox.Visibility = Visibility.Visible;

                Bip39PassphrasePasswordBox.Visibility = Visibility.Collapsed;
                ConfirmBip39PassphrasePasswordBox.Visibility = Visibility.Collapsed;

                Bip39PassphraseTextBox.Focus();
                Bip39PassphraseTextBox.CaretIndex = Bip39PassphraseTextBox.Text.Length;
            }
            else
            {
                Bip39PassphrasePasswordBox.Password = Bip39PassphraseTextBox.Text;
                ConfirmBip39PassphrasePasswordBox.Password = ConfirmBip39PassphraseTextBox.Text;

                Bip39PassphrasePasswordBox.Visibility = Visibility.Visible;
                ConfirmBip39PassphrasePasswordBox.Visibility = Visibility.Visible;

                Bip39PassphraseTextBox.Visibility = Visibility.Collapsed;
                ConfirmBip39PassphraseTextBox.Visibility = Visibility.Collapsed;

                Bip39PassphrasePasswordBox.Focus();
            }

            _bip39Passphrase = Bip39PassphrasePasswordBox.Visibility == Visibility.Visible
                ? Bip39PassphrasePasswordBox.Password
                : Bip39PassphraseTextBox.Text;

            _confirmBip39Passphrase = ConfirmBip39PassphrasePasswordBox.Visibility == Visibility.Visible
                ? ConfirmBip39PassphrasePasswordBox.Password
                : ConfirmBip39PassphraseTextBox.Text;
        }

        private void Bip39PassphrasePasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (Bip39PassphrasePasswordBox.Visibility == Visibility.Visible)
                _bip39Passphrase = Bip39PassphrasePasswordBox.Password;
        }

        private void Bip39PassphraseTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Bip39PassphraseTextBox.Visibility == Visibility.Visible)
                _bip39Passphrase = Bip39PassphraseTextBox.Text;
        }

        private void ConfirmBip39PassphrasePasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ConfirmBip39PassphrasePasswordBox.Visibility == Visibility.Visible)
                _confirmBip39Passphrase = ConfirmBip39PassphrasePasswordBox.Password;
        }

        private void ConfirmBip39PassphraseTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ConfirmBip39PassphraseTextBox.Visibility == Visibility.Visible)
                _confirmBip39Passphrase = ConfirmBip39PassphraseTextBox.Text;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            CloseOverlay();
        }



        private void CloseOverlay()
        {
            var main = Window.GetWindow(this) as MainWindow;
            if (main == null)
                return;

            if (HasGeneratedWallet())
            {
                var confirm = MessageBox.Show(
                    "Make sure you have written down your seed phrase.\n\nClose anyway?",
                    "Confirm",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes)
                    return;
            }

            main.CloseModalOverlay();
        }

        private bool HasGeneratedWallet()
        {
            return _mnemonicWords.Count > 0 ||
                   !string.IsNullOrWhiteSpace(_generatedAddress) ||
                   !string.IsNullOrWhiteSpace(_generatedAddress0x) ||
                   !string.IsNullOrWhiteSpace(_generatedPrivateKey);
        }

        private void ClearGeneratedWalletState(bool resetLayoutOnly)
        {
            _mnemonicWords.Clear();
            _generatedPrivateKey = string.Empty;
            _generatedAddress0x = string.Empty;
            _generatedAddress = string.Empty;

            OutputTextBlock.Text = "Address and derivation path will appear here...";
            CopyAddressButton.IsEnabled = false;
            CopyRecoveryPhraseButton.IsEnabled = false;
            GenerateKeystoreButton.IsEnabled = false;

            ShowRecoveryPhraseCheckBox.IsChecked = false;
            ShowRecoveryPhraseCheckBox.IsEnabled = false;

            if (!resetLayoutOnly)
            {
                MnemonicGridControl.Clear();
            }
            else
            {
                MnemonicGridControl.SetWordCount(GetSelectedWordCount());
                MnemonicGridControl.Clear();
            }
        }

        private int GetSelectedWordCount()
        {
            if (PhraseLengthComboBox.SelectedItem is ComboBoxItem item &&
                int.TryParse(item.Tag?.ToString(), out var wc))
                return wc;

            return 12;
        }

        private string GetSelectedDerivationPath()
        {
            return DerivationPathComboBox.SelectedIndex == 1
                ? "m/44'/60'/0'/0/0"
                : "m/44'/550'/0'/0/0";
        }
    }
}
































