using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Nethereum.KeyStore;
using Nethereum.Signer;
using Nethereum.Web3;
using XdcLocalDesktopAccessTool.App.Services;

namespace XdcLocalDesktopAccessTool.App.Views.Windows
{
    public partial class AuthorisationWindow : Window
    {
        private readonly ObservableCollection<WordEntry> _wordEntries = new();
        private readonly ObservableCollection<DerivedAddressRow> _derivedRows = new();
        private DerivedAddressRow? _selectedDerivedRow;

        private bool _isWindowReady;
        private bool _suppressUiEvents;

        private int _lastAuthTabIndex;
        private int _lastPhraseLengthIndex;
        private int _lastDerivationPathIndex;

        private string _privateKeyValidatedHex0x = string.Empty;
        private string _privateKeyValidatedAddress0x = string.Empty;
        private string _privateKeyValidatedAddressXdc = string.Empty;

        public AuthorisationWindow()
        {
            _isWindowReady = false;
            _suppressUiEvents = false;

            InitializeComponent();

            if (WordsItemsControl != null)
                WordsItemsControl.ItemsSource = _wordEntries;

            if (DerivedAddressesDataGrid != null)
                DerivedAddressesDataGrid.ItemsSource = _derivedRows;

            InitialiseRecoveryPhraseUi();
            InitialisePrivateKeyUi();

            _lastAuthTabIndex = AuthTabControl?.SelectedIndex ?? 0;
            _lastPhraseLengthIndex = PhraseLengthComboBox?.SelectedIndex ?? 0;
            _lastDerivationPathIndex = DerivationPathComboBox?.SelectedIndex ?? 0;

            _isWindowReady = true;
            UpdateAuthoriseEnabled();
            UpdateSelectedAddressSummary();
        }

        private void BrowseKeystore_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select keystore JSON file",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dlg.ShowDialog(this) == true)
            {
                if (KeystorePathTextBox != null)
                    KeystorePathTextBox.Text = dlg.FileName;

                UpdateAuthoriseEnabled();
            }
        }

        private void ShowPasswordCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (KeystorePasswordTextBox == null || KeystorePasswordBox == null)
                return;

            KeystorePasswordTextBox.Text = KeystorePasswordBox.Password;
            KeystorePasswordBox.Visibility = Visibility.Collapsed;
            KeystorePasswordTextBox.Visibility = Visibility.Visible;
            KeystorePasswordTextBox.Focus();
            KeystorePasswordTextBox.CaretIndex = KeystorePasswordTextBox.Text.Length;
            UpdateAuthoriseEnabled();
        }

        private void ShowPasswordCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (KeystorePasswordTextBox == null || KeystorePasswordBox == null)
                return;

            KeystorePasswordBox.Password = KeystorePasswordTextBox.Text;
            KeystorePasswordTextBox.Visibility = Visibility.Collapsed;
            KeystorePasswordBox.Visibility = Visibility.Visible;
            KeystorePasswordBox.Focus();
            UpdateAuthoriseEnabled();
        }

        private void AuthInputs_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            UpdateAuthoriseEnabled();
        }

        private void InitialiseRecoveryPhraseUi()
        {
            if (PhraseLengthComboBox != null) PhraseLengthComboBox.SelectedIndex = 0;
            if (DerivationPathComboBox != null) DerivationPathComboBox.SelectedIndex = 0;
            if (ScanCountComboBox != null) ScanCountComboBox.SelectedIndex = 0;

            if (UsePassphraseCheckBox != null) UsePassphraseCheckBox.IsChecked = false;
            if (ShowPhraseCheckBox != null) ShowPhraseCheckBox.IsChecked = false;

            if (PassphraseRowGrid != null) PassphraseRowGrid.Visibility = Visibility.Hidden;
            if (ShowBip39PassphraseCheckBox != null)
            {
                ShowBip39PassphraseCheckBox.IsChecked = false;
                ShowBip39PassphraseCheckBox.IsEnabled = false;
            }

            if (PassphrasePasswordBox != null) PassphrasePasswordBox.Password = string.Empty;
            if (PassphraseTextBox != null) PassphraseTextBox.Text = string.Empty;

            SwapPassphraseVisibility(showPlainText: false);
            BuildWordEntries(12);

            ClearDerivedSelectionAndRows();
        }

        private void InitialisePrivateKeyUi()
        {
            if (ShowPrivateKeyCheckBox != null)
                ShowPrivateKeyCheckBox.IsChecked = false;

            if (PrivateKeyPasswordBox != null)
                PrivateKeyPasswordBox.Password = string.Empty;

            if (PrivateKeyTextBox != null)
                PrivateKeyTextBox.Text = string.Empty;

            if (PrivateKeyTextBox != null)
                PrivateKeyTextBox.Visibility = Visibility.Collapsed;

            if (PrivateKeyPasswordBox != null)
                PrivateKeyPasswordBox.Visibility = Visibility.Visible;

            ClearPrivateKeyValidationState(clearInputs: false);

            if (PrivateKeyValidationStatusTextBlock != null)
            {
                PrivateKeyValidationStatusTextBlock.Text = "Enter a private key, then click Validate.";
                PrivateKeyValidationStatusTextBlock.Foreground = Brushes.DimGray;
            }
        }

        private void ShowPrivateKeyCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (PrivateKeyPasswordBox == null || PrivateKeyTextBox == null)
                return;

            PrivateKeyTextBox.Text = PrivateKeyPasswordBox.Password;
            PrivateKeyPasswordBox.Visibility = Visibility.Collapsed;
            PrivateKeyTextBox.Visibility = Visibility.Visible;
            PrivateKeyTextBox.Focus();
            PrivateKeyTextBox.CaretIndex = PrivateKeyTextBox.Text.Length;
        }

        private void ShowPrivateKeyCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (PrivateKeyPasswordBox == null || PrivateKeyTextBox == null)
                return;

            PrivateKeyPasswordBox.Password = PrivateKeyTextBox.Text ?? string.Empty;
            PrivateKeyTextBox.Visibility = Visibility.Collapsed;
            PrivateKeyPasswordBox.Visibility = Visibility.Visible;
            PrivateKeyPasswordBox.Focus();
        }

        private void PrivateKeyInputs_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            ClearPrivateKeyValidationState(clearInputs: false);

            var current = GetCurrentPrivateKeyInput();
            bool hasAnyText = !string.IsNullOrEmpty(current);
            bool formatLooksValid = IsPrivateKeyFormatValid(current);

            if (PrivateKeyFormatTickTextBlock != null)
                PrivateKeyFormatTickTextBlock.Visibility = formatLooksValid ? Visibility.Visible : Visibility.Collapsed;

            if (ValidatePrivateKeyButton != null)
                ValidatePrivateKeyButton.IsEnabled = formatLooksValid;

            if (PrivateKeyValidationStatusTextBlock != null)
            {
                if (!hasAnyText)
                {
                    PrivateKeyValidationStatusTextBlock.Text = "Enter a private key, then click Validate.";
                    PrivateKeyValidationStatusTextBlock.Foreground = Brushes.DimGray;
                }
                else if (!formatLooksValid)
                {
                    PrivateKeyValidationStatusTextBlock.Text = "Invalid private key format.";
                    PrivateKeyValidationStatusTextBlock.Foreground = Brushes.Firebrick;
                }
                else
                {
                    PrivateKeyValidationStatusTextBlock.Text = "Private key format looks valid. Click Validate.";
                    PrivateKeyValidationStatusTextBlock.Foreground = Brushes.DimGray;
                }
            }

            UpdateAuthoriseEnabled();
        }

        private string GetCurrentPrivateKeyInput()
        {
            if (ShowPrivateKeyCheckBox?.IsChecked == true)
                return PrivateKeyTextBox?.Text ?? string.Empty;

            return PrivateKeyPasswordBox?.Password ?? string.Empty;
        }

        private static bool IsPrivateKeyFormatValid(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            string value = input.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? input.Substring(2)
                : input;

            return value.Length == 64 && value.All(Uri.IsHexDigit);
        }

        private void ValidatePrivateKeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            var raw = GetCurrentPrivateKeyInput();
            if (!IsPrivateKeyFormatValid(raw))
            {
                if (PrivateKeyValidationStatusTextBlock != null)
                {
                    PrivateKeyValidationStatusTextBlock.Text = "Invalid private key format.";
                    PrivateKeyValidationStatusTextBlock.Foreground = Brushes.Firebrick;
                }

                ClearPrivateKeyValidationState(clearInputs: false);
                return;
            }

            try
            {
                var privateKeyHex0x = Ensure0x(raw);
                var ecKey = new EthECKey(privateKeyHex0x);
                var address0x = ecKey.GetPublicAddress();
                var addressXdc = AddressFormat.ToXdcAddress(address0x);

                _privateKeyValidatedHex0x = privateKeyHex0x;
                _privateKeyValidatedAddress0x = address0x;
                _privateKeyValidatedAddressXdc = addressXdc;

                if (PrivateKeyAddressTextBox != null)
                    PrivateKeyAddressTextBox.Text = addressXdc;

                if (PrivateKeyValidationStatusTextBlock != null)
                {
                    PrivateKeyValidationStatusTextBlock.Text = "Private key validated.";
                    PrivateKeyValidationStatusTextBlock.Foreground = Brushes.DimGray;
                }

                if (CopyPrivateKeyAddressButton != null)
                    CopyPrivateKeyAddressButton.IsEnabled = true;

                if (CreateKeystoreFromPrivateKeyButton != null)
                    CreateKeystoreFromPrivateKeyButton.IsEnabled = true;

                UpdateAuthoriseEnabled();
            }
            catch
            {
                if (PrivateKeyValidationStatusTextBlock != null)
                {
                    PrivateKeyValidationStatusTextBlock.Text = "Unable to validate private key.";
                    PrivateKeyValidationStatusTextBlock.Foreground = Brushes.Firebrick;
                }

                ClearPrivateKeyValidationState(clearInputs: false);
            }
        }

        private async void CopyPrivateKeyAddressButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_privateKeyValidatedAddressXdc))
                return;

            try
            {
                Clipboard.SetText(_privateKeyValidatedAddressXdc);

                if (sender is Button btn)
                {
                    var original = btn.Content;
                    btn.Content = "Copied";
                    btn.IsEnabled = false;

                    await System.Threading.Tasks.Task.Delay(1000);

                    btn.Content = original;
                    btn.IsEnabled = !string.IsNullOrWhiteSpace(_privateKeyValidatedAddressXdc);
                }
            }
            catch
            {
                MessageBox.Show(
                    "Could not copy address.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void CreateKeystoreFromPrivateKeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            if (string.IsNullOrWhiteSpace(_privateKeyValidatedHex0x) ||
                string.IsNullOrWhiteSpace(_privateKeyValidatedAddress0x) ||
                string.IsNullOrWhiteSpace(_privateKeyValidatedAddressXdc))
            {
                MessageBox.Show(
                    "Please validate a private key first.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                "You are about to open the keystore creation window for this address.\n\n" +
                $"Address:\n{_privateKeyValidatedAddressXdc}\n\n" +
                "Do you want to continue?",
                "Create keystore file",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            var win = new GenerateKeystoreFileWindow
            {
                Owner = this
            };

            win.SetWalletContext(
                _privateKeyValidatedAddressXdc,
                _privateKeyValidatedAddress0x,
                _privateKeyValidatedHex0x,
                "Direct private key import",
                false);

            win.ShowDialog();
        }

        private bool IsPrivateKeySectionDirty()
        {
            return !string.IsNullOrEmpty(GetCurrentPrivateKeyInput()) ||
                   !string.IsNullOrWhiteSpace(_privateKeyValidatedAddressXdc) ||
                   !string.IsNullOrWhiteSpace(PrivateKeyAddressTextBox?.Text);
        }

        private void ClearPrivateKeyValidationState(bool clearInputs)
        {
            _privateKeyValidatedHex0x = string.Empty;
            _privateKeyValidatedAddress0x = string.Empty;
            _privateKeyValidatedAddressXdc = string.Empty;

            if (PrivateKeyAddressTextBox != null)
                PrivateKeyAddressTextBox.Text = string.Empty;

            if (CopyPrivateKeyAddressButton != null)
                CopyPrivateKeyAddressButton.IsEnabled = false;

            if (CreateKeystoreFromPrivateKeyButton != null)
                CreateKeystoreFromPrivateKeyButton.IsEnabled = false;

            if (clearInputs)
            {
                if (PrivateKeyPasswordBox != null)
                    PrivateKeyPasswordBox.Password = string.Empty;

                if (PrivateKeyTextBox != null)
                    PrivateKeyTextBox.Text = string.Empty;

                if (ShowPrivateKeyCheckBox != null)
                    ShowPrivateKeyCheckBox.IsChecked = false;

                if (PrivateKeyTextBox != null)
                    PrivateKeyTextBox.Visibility = Visibility.Collapsed;

                if (PrivateKeyPasswordBox != null)
                    PrivateKeyPasswordBox.Visibility = Visibility.Visible;
            }
        }

        private bool ConfirmLeavePrivateKeySection(string message)
        {
            if (!IsPrivateKeySectionDirty())
                return true;

            var result = MessageBox.Show(
                message,
                "Confirm change",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return false;

            ClearPrivateKeyValidationState(clearInputs: true);

            if (PrivateKeyFormatTickTextBlock != null)
                PrivateKeyFormatTickTextBlock.Visibility = Visibility.Collapsed;

            if (ValidatePrivateKeyButton != null)
                ValidatePrivateKeyButton.IsEnabled = false;

            if (PrivateKeyValidationStatusTextBlock != null)
            {
                PrivateKeyValidationStatusTextBlock.Text = "Enter a private key, then click Validate.";
                PrivateKeyValidationStatusTextBlock.Foreground = Brushes.DimGray;
            }

            return true;
        }

        private void AuthoriseFromPrivateKey()
        {
            if (string.IsNullOrWhiteSpace(_privateKeyValidatedHex0x) ||
                string.IsNullOrWhiteSpace(_privateKeyValidatedAddress0x))
            {
                MessageBox.Show(
                    "Please validate a private key before authorising.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                AppSession.Instance.SetWallet(_privateKeyValidatedHex0x, _privateKeyValidatedAddress0x);
                ClearPrivateKeyValidationState(clearInputs: true);

                if (PrivateKeyFormatTickTextBlock != null)
                    PrivateKeyFormatTickTextBlock.Visibility = Visibility.Collapsed;

                if (ValidatePrivateKeyButton != null)
                    ValidatePrivateKeyButton.IsEnabled = false;

                if (PrivateKeyValidationStatusTextBlock != null)
                {
                    PrivateKeyValidationStatusTextBlock.Text = "Enter a private key, then click Validate.";
                    PrivateKeyValidationStatusTextBlock.Foreground = Brushes.DimGray;
                }

                UpdateAuthoriseEnabled();

                DialogResult = true;
                Close();
            }
            catch
            {
                MessageBox.Show(
                    "Unable to authorise from private key. Please check the key and try again.",
                    "Authorisation Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ClearKeystoreSection()
        {
            if (KeystorePathTextBox != null)
                KeystorePathTextBox.Text = string.Empty;

            if (KeystorePasswordBox != null)
                KeystorePasswordBox.Password = string.Empty;

            if (KeystorePasswordTextBox != null)
                KeystorePasswordTextBox.Text = string.Empty;

            if (ShowPasswordCheckBox != null)
                ShowPasswordCheckBox.IsChecked = false;

            if (KeystorePasswordTextBox != null)
                KeystorePasswordTextBox.Visibility = Visibility.Collapsed;

            if (KeystorePasswordBox != null)
                KeystorePasswordBox.Visibility = Visibility.Visible;

            UpdateAuthoriseEnabled();
        }

        private void AuthTabControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents || AuthTabControl == null)
                return;

            var dep = e.OriginalSource as DependencyObject;
            if (dep == null)
                return;

            var tabItem = FindAncestor<TabItem>(dep);
            if (tabItem == null)
                return;

            var newIndex = AuthTabControl.Items.IndexOf(tabItem);
            if (newIndex < 0 || newIndex == _lastAuthTabIndex)
                return;

            bool canLeave = true;

            if (_lastAuthTabIndex == 0)
            {
                ClearKeystoreSection();
            }
            else if (_lastAuthTabIndex == 1)
            {
                canLeave = ConfirmLeaveRecoverySection(
                    "Changing this screen will clear the current recovery phrase, BIP39 passphrase, and any scanned or selected addresses.\n\nDo you want to continue?");
            }
            else if (_lastAuthTabIndex == 2)
            {
                canLeave = ConfirmLeavePrivateKeySection(
                    "Changing this screen will clear the current private key information.\n\nDo you want to continue?");
            }

            if (!canLeave)
            {
                e.Handled = true;
                return;
            }

            e.Handled = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (AuthTabControl == null)
                    return;

                _suppressUiEvents = true;
                AuthTabControl.SelectedIndex = newIndex;
                _suppressUiEvents = false;

                _lastAuthTabIndex = newIndex;
                UpdateAuthoriseEnabled();
            }));
        }

        private void AuthTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents || AuthTabControl == null)
                return;

            if (_lastAuthTabIndex == 0 && AuthTabControl.SelectedIndex != 0)
                ClearKeystoreSection();

            _lastAuthTabIndex = AuthTabControl.SelectedIndex;
            UpdateAuthoriseEnabled();
        }

        private void PhraseLengthComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents || PhraseLengthComboBox == null)
                return;

            var newIndex = PhraseLengthComboBox.SelectedIndex;
            if (newIndex == _lastPhraseLengthIndex)
            {
                UpdateAuthoriseEnabled();
                return;
            }

            if (!ConfirmLeaveRecoverySection(
                    "Changing phrase length will clear the current recovery phrase, BIP39 passphrase, and any scanned or selected addresses.\n\nDo you want to continue?"))
            {
                _suppressUiEvents = true;
                PhraseLengthComboBox.SelectedIndex = _lastPhraseLengthIndex;
                _suppressUiEvents = false;
                UpdateAuthoriseEnabled();
                return;
            }

            var want24 = newIndex == 1;
            BuildWordEntries(want24 ? 24 : 12);

            _lastPhraseLengthIndex = newIndex;
            UpdateAuthoriseEnabled();
        }

        private void RecoveryInputs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            if (!ReferenceEquals(sender, DerivationPathComboBox))
            {
                UpdateAuthoriseEnabled();
                return;
            }

            if (DerivationPathComboBox == null)
                return;

            var newIndex = DerivationPathComboBox.SelectedIndex;
            if (newIndex == _lastDerivationPathIndex)
            {
                UpdateAuthoriseEnabled();
                return;
            }

            _lastDerivationPathIndex = newIndex;

            if (_derivedRows.Count > 0 || _selectedDerivedRow != null)
            {
                ClearDerivedSelectionAndRows();

                MessageBox.Show(
                    "The derivation path has changed.\n\nYou will need to click Scan now again to view addresses for the new path.",
                    "Derivation Path Changed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            UpdateAuthoriseEnabled();
        }

        private void RecoveryInputs_RoutedChanged(object sender, RoutedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            UpdateAuthoriseEnabled();
        }

        private void UsePassphraseCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            var enabled = UsePassphraseCheckBox != null && UsePassphraseCheckBox.IsChecked == true;

            if (PassphraseRowGrid != null)
                PassphraseRowGrid.Visibility = enabled ? Visibility.Visible : Visibility.Hidden;

            if (ShowBip39PassphraseCheckBox != null)
            {
                ShowBip39PassphraseCheckBox.IsEnabled = enabled;
                ShowBip39PassphraseCheckBox.IsChecked = false;
            }

            if (enabled)
            {
                MessageBox.Show(
                    "The BIP39 passphrase acts as a second password.\n\n" +
                    "If a different passphrase is entered, a completely different wallet will be generated.\n\n" +
                    "Make sure you enter the exact passphrase used when the wallet was originally created.",
                    "BIP39 Passphrase",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            if (!enabled)
            {
                if (PassphrasePasswordBox != null) PassphrasePasswordBox.Password = string.Empty;
                if (PassphraseTextBox != null) PassphraseTextBox.Text = string.Empty;
                SwapPassphraseVisibility(showPlainText: false);
            }

            UpdateAuthoriseEnabled();
        }

        private void ShowBip39PassphraseCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            SwapPassphraseVisibility(showPlainText: true);
        }

        private void ShowBip39PassphraseCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            SwapPassphraseVisibility(showPlainText: false);
        }

        private void SwapPassphraseVisibility(bool showPlainText)
        {
            if (PassphrasePasswordBox == null || PassphraseTextBox == null)
                return;

            if (showPlainText)
            {
                PassphraseTextBox.Text = PassphrasePasswordBox.Password;
                PassphraseTextBox.Visibility = Visibility.Visible;
                PassphrasePasswordBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                PassphrasePasswordBox.Password = PassphraseTextBox.Text ?? string.Empty;
                PassphrasePasswordBox.Visibility = Visibility.Visible;
                PassphraseTextBox.Visibility = Visibility.Collapsed;
            }
        }

        private void PassphrasePasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            if (PassphrasePasswordBox == null || PassphraseTextBox == null)
                return;

            if (PassphrasePasswordBox.Visibility == Visibility.Visible)
                PassphraseTextBox.Text = PassphrasePasswordBox.Password;

            UpdateAuthoriseEnabled();
        }

        private void PassphraseTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            if (PassphrasePasswordBox == null || PassphraseTextBox == null)
                return;

            if (PassphraseTextBox.Visibility == Visibility.Visible)
                PassphrasePasswordBox.Password = PassphraseTextBox.Text ?? string.Empty;

            UpdateAuthoriseEnabled();
        }

        private void BuildWordEntries(int expectedWordCount)
        {
            _wordEntries.Clear();

            if (expectedWordCount == 12)
            {
                int wordNum = 1;

                for (int i = 0; i < 24; i++)
                {
                    bool isSpacer =
                        (i >= 4 && i <= 7) ||
                        (i >= 12 && i <= 15) ||
                        (i >= 20 && i <= 23);

                    if (isSpacer)
                    {
                        _wordEntries.Add(new WordEntry(i, true, string.Empty));
                        continue;
                    }

                    _wordEntries.Add(new WordEntry(i, false, wordNum.ToString(CultureInfo.InvariantCulture)));
                    wordNum++;
                }
            }
            else
            {
                for (int i = 0; i < 24; i++)
                    _wordEntries.Add(new WordEntry(i, false, (i + 1).ToString(CultureInfo.InvariantCulture)));
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                SyncWordBoxesToModel();
                ApplyMaskingFromCheckbox();
            }));
        }

        private void ShowPhraseCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            SyncWordBoxesToModel();
            ApplyMaskingFromCheckbox();
        }

        private void ApplyMaskingFromCheckbox()
        {
            try
            {
                if (WordsItemsControl == null || ShowPhraseCheckBox == null)
                    return;

                var alwaysShow = ShowPhraseCheckBox.IsChecked == true;

                for (int i = 0; i < _wordEntries.Count; i++)
                {
                    if (_wordEntries[i].IsPlaceholder) continue;

                    var container = WordsItemsControl.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                    if (container == null) continue;

                    var cell = FindChild<Border>(container, "WordCellRoot");
                    if (cell == null) continue;

                    SetWordCellMasked(cell, masked: !alwaysShow);
                }
            }
            catch
            {
            }
        }

        private void WordPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            if (sender is not PasswordBox pb) return;
            if (pb.DataContext is not WordEntry entry) return;
            if (entry.IsPlaceholder) return;

            entry.Text = pb.Password ?? string.Empty;
            UpdateAuthoriseEnabled();
        }

        private void WordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            if (sender is not TextBox tb) return;
            if (tb.DataContext is not WordEntry entry) return;
            if (entry.IsPlaceholder) return;

            entry.Text = tb.Text ?? string.Empty;
            UpdateAuthoriseEnabled();
        }

        private void WordBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            if (ShowPhraseCheckBox != null && ShowPhraseCheckBox.IsChecked == true)
                return;

            var cell = FindWordCellRoot(sender as DependencyObject);
            if (cell == null) return;

            if (cell.DataContext is WordEntry entry && entry.IsPlaceholder)
                return;

            SetWordCellMasked(cell, masked: false);

            if (sender is PasswordBox)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var tb = FindChild<TextBox>(cell, "WordTextBox");
                    if (tb == null) return;

                    tb.Focus();
                    tb.CaretIndex = tb.Text?.Length ?? 0;
                }));
            }
        }

        private void WordBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            if (ShowPhraseCheckBox != null && ShowPhraseCheckBox.IsChecked == true)
                return;

            var cell = FindWordCellRoot(sender as DependencyObject);
            if (cell == null) return;

            if (cell.DataContext is WordEntry entry && entry.IsPlaceholder)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                var focused = Keyboard.FocusedElement as DependencyObject;
                if (focused != null && IsDescendant(cell, focused))
                    return;

                SetWordCellMasked(cell, masked: true);
            }));
        }

        private static Border? FindWordCellRoot(DependencyObject? sender)
        {
            var current = sender;
            while (current != null)
            {
                if (current is Border b && b.Name == "WordCellRoot")
                    return b;

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void SetWordCellMasked(FrameworkElement cellRoot, bool masked)
        {
            if (cellRoot.DataContext is WordEntry entry && entry.IsPlaceholder)
                return;

            var tb = FindChild<TextBox>(cellRoot, "WordTextBox");
            var pb = FindChild<PasswordBox>(cellRoot, "WordPasswordBox");
            if (tb == null || pb == null) return;

            if (!masked)
            {
                if (tb.Text != pb.Password)
                    tb.Text = pb.Password;

                tb.Visibility = Visibility.Visible;
                pb.Visibility = Visibility.Hidden;
            }
            else
            {
                if (pb.Password != tb.Text)
                    pb.Password = tb.Text ?? string.Empty;

                tb.Visibility = Visibility.Hidden;
                pb.Visibility = Visibility.Visible;
            }
        }

        private void SyncWordBoxesToModel()
        {
            try
            {
                if (WordsItemsControl == null)
                    return;

                for (int i = 0; i < _wordEntries.Count; i++)
                {
                    if (_wordEntries[i].IsPlaceholder) continue;

                    var container = WordsItemsControl.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                    if (container == null) continue;

                    var cell = FindChild<Border>(container, "WordCellRoot");
                    if (cell == null) continue;

                    var pb = FindChild<PasswordBox>(cell, "WordPasswordBox");
                    var tb = FindChild<TextBox>(cell, "WordTextBox");

                    var text = _wordEntries[i].Text ?? string.Empty;

                    if (pb != null && pb.Password != text)
                        pb.Password = text;

                    if (tb != null && tb.Text != text)
                        tb.Text = text;
                }
            }
            catch
            {
            }
        }

        private static T? FindChild<T>(DependencyObject parent, string childName) where T : FrameworkElement
        {
            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T fe && fe.Name == childName)
                    return fe;

                var result = FindChild<T>(child, childName);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match)
                    return match;

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private static bool IsDescendant(DependencyObject ancestor, DependencyObject node)
        {
            var current = node;
            while (current != null)
            {
                if (ReferenceEquals(current, ancestor))
                    return true;

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void UpdateSelectedAddressSummary()
        {
            if (SelectedAddressSummaryTextBlock == null)
                return;

            if (_selectedDerivedRow == null)
            {
                SelectedAddressSummaryTextBlock.Text = "Selected address: None";
                return;
            }

            SelectedAddressSummaryTextBlock.Text =
                $"Selected address: {_selectedDerivedRow.AddressXdc}";
        }

        private bool IsRecoverySectionDirty()
        {
            if (_wordEntries.Any(w => !w.IsPlaceholder && !string.IsNullOrWhiteSpace(w.Text)))
                return true;

            if (!string.IsNullOrWhiteSpace(PassphrasePasswordBox?.Password))
                return true;

            if (!string.IsNullOrWhiteSpace(PassphraseTextBox?.Text))
                return true;

            if (_derivedRows.Count > 0)
                return true;

            if (_selectedDerivedRow != null)
                return true;

            return false;
        }

        private void ClearRecoverySection()
        {
            foreach (var w in _wordEntries)
                w.Text = string.Empty;

            if (PassphrasePasswordBox != null)
                PassphrasePasswordBox.Password = string.Empty;

            if (PassphraseTextBox != null)
                PassphraseTextBox.Text = string.Empty;

            if (UsePassphraseCheckBox != null)
                UsePassphraseCheckBox.IsChecked = false;

            if (ShowPhraseCheckBox != null)
                ShowPhraseCheckBox.IsChecked = false;

            SyncWordBoxesToModel();

            if (PhraseLengthComboBox != null)
                PhraseLengthComboBox.SelectedIndex = 0;

            if (DerivationPathComboBox != null)
                DerivationPathComboBox.SelectedIndex = 0;

            if (ScanCountComboBox != null)
                ScanCountComboBox.SelectedIndex = 0;

            _lastPhraseLengthIndex = PhraseLengthComboBox?.SelectedIndex ?? 0;
            _lastDerivationPathIndex = DerivationPathComboBox?.SelectedIndex ?? 0;

            ClearDerivedSelectionAndRows();
        }

        private bool ConfirmLeaveRecoverySection(string message)
        {
            if (!IsRecoverySectionDirty())
                return true;

            var result = MessageBox.Show(
                message,
                "Confirm change",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return false;

            ClearRecoverySection();
            return true;
        }

        private void ClearDerivedSelectionAndRows()
        {
            _selectedDerivedRow = null;
            _derivedRows.Clear();

            if (DerivedAddressesDataGrid != null)
                DerivedAddressesDataGrid.SelectedItem = null;

            if (CreateKeystoreSelectedButton != null)
                CreateKeystoreSelectedButton.IsEnabled = false;

            if (CopySelectedAddressButton != null)
                CopySelectedAddressButton.IsEnabled = false;

            if (ExportPrivateKeySelectedButton != null)
                ExportPrivateKeySelectedButton.IsEnabled = false;

            UpdateSelectedAddressSummary();
        }

        private string BuildMnemonicFromInputs(out bool wordCountOk)
        {
            var words = _wordEntries
                .Where(w => !w.IsPlaceholder)
                .Select(w => (w.Text ?? string.Empty).Trim().ToLowerInvariant())
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .ToArray();

            var expected = (PhraseLengthComboBox != null && PhraseLengthComboBox.SelectedIndex == 1) ? 24 : 12;
            wordCountOk = words.Length == expected;

            return string.Join(' ', words);
        }

        private string GetSelectedDerivationPathTemplate()
        {
            if (DerivationPathComboBox?.SelectedItem is ComboBoxItem item)
            {
                var tag = item.Tag?.ToString();
                if (!string.IsNullOrWhiteSpace(tag))
                    return tag.Trim();
            }

            return "m/44'/550'/0'/0/{i}";
        }

        private int GetScanCount()
        {
            var text = (ScanCountComboBox?.SelectedItem as ComboBoxItem)?.Content?.ToString()
                       ?? ScanCountComboBox?.Text
                       ?? "10";

            return int.TryParse(text, out var n) ? n : 10;
        }

        private string GetRpcUrlForScanning()
        {
            var url = AppSession.Instance.CurrentRpcUrl;
            if (!string.IsNullOrWhiteSpace(url))
                return url;

            return AppSession.Instance.GetDefaultRpc();
        }

        private string GetPassphraseOrEmpty()
        {
            if (UsePassphraseCheckBox != null && UsePassphraseCheckBox.IsChecked == true)
            {
                if (PassphrasePasswordBox != null && PassphrasePasswordBox.Visibility == Visibility.Visible)
                    return PassphrasePasswordBox.Password ?? string.Empty;

                return PassphraseTextBox?.Text ?? string.Empty;
            }

            return string.Empty;
        }

        private bool ValidateBip39PassphraseIfEnabled()
        {
            if (UsePassphraseCheckBox == null || UsePassphraseCheckBox.IsChecked != true)
                return true;

            var passphrase = GetPassphraseOrEmpty().Trim();
            if (!string.IsNullOrWhiteSpace(passphrase))
                return true;

            MessageBox.Show(
                "BIP39 passphrase is enabled but no passphrase was entered.",
                "Validation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return false;
        }

        private async void ScanNowButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            ClearDerivedSelectionAndRows();
            UpdateAuthoriseEnabled();

            var mnemonic = BuildMnemonicFromInputs(out var wordCountOk);
            if (!wordCountOk)
            {
                MessageBox.Show(
                    "Please enter a complete 12 or 24 word recovery phrase.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!ValidateBip39PassphraseIfEnabled())
                return;

            try
            {
                _ = WalletDerivationService.DeriveFromMnemonic(
                    mnemonicPhrase: mnemonic,
                    bip39Passphrase: GetPassphraseOrEmpty(),
                    derivationPathTemplate: GetSelectedDerivationPathTemplate(),
                    index: 0);
            }
            catch
            {
                MessageBox.Show(
                    "Invalid recovery phrase. Please check the words and try again.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var pathTemplate = GetSelectedDerivationPathTemplate();
            var count = GetScanCount();
            var rpcUrl = GetRpcUrlForScanning();

            if (ScanNowButton != null)
                ScanNowButton.IsEnabled = false;

            try
            {
                var web3 = new Web3(rpcUrl);

                var derivedAccounts = WalletDerivationService.DeriveManyFromMnemonic(
                    mnemonicPhrase: mnemonic,
                    bip39Passphrase: GetPassphraseOrEmpty(),
                    derivationPathTemplate: pathTemplate,
                    count: count);

                foreach (var acct in derivedAccounts)
                {
                    string balanceXdcText = "0";

                    try
                    {
                        var balWei = await web3.Eth.GetBalance.SendRequestAsync(acct.Address0x);
                        balanceXdcText = Web3.Convert.FromWei(balWei)
                            .ToString("0.#########", CultureInfo.InvariantCulture);
                    }
                    catch
                    {
                        balanceXdcText = "—";
                    }

                    _derivedRows.Add(new DerivedAddressRow
                    {
                        Index = acct.Index,
                        Address0x = acct.Address0x,
                        AddressXdc = acct.AddressXdc,
                        BalanceXdc = balanceXdcText,
                        PrivateKeyHex0x = acct.PrivateKeyHex0x,
                        DerivationPathUsed = acct.DerivationPathUsed
                    });
                }

                if (DerivedAddressesDataGrid != null)
                {
                    DerivedAddressesDataGrid.SelectedItem = null;
                    DerivedAddressesDataGrid.UpdateLayout();

                    if (_derivedRows.Count > 0)
                        DerivedAddressesDataGrid.ScrollIntoView(_derivedRows[0]);
                }
            }
            finally
            {
                if (ScanNowButton != null)
                    ScanNowButton.IsEnabled = true;
            }
        }

        private void DerivedAddressesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            _selectedDerivedRow = DerivedAddressesDataGrid?.SelectedItem as DerivedAddressRow;
            UpdateSelectedAddressSummary();
            UpdateAuthoriseEnabled();
        }

        private async void CopySelectedAddressButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedDerivedRow == null)
                return;

            try
            {
                Clipboard.SetText(_selectedDerivedRow.AddressXdc);

                if (sender is Button btn)
                {
                    var original = btn.Content;
                    btn.Content = "Copied";
                    btn.IsEnabled = false;

                    await System.Threading.Tasks.Task.Delay(1000);

                    btn.Content = original;
                    btn.IsEnabled = _selectedDerivedRow != null;
                }
            }
            catch
            {
                MessageBox.Show(
                    "Could not copy address.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ExportPrivateKeySelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            if (_selectedDerivedRow == null)
            {
                MessageBox.Show(
                    "Please select an address first.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                "Exporting a private key gives full control of this wallet.\n\n" +
                "Anyone who gets this private key can move the funds.\n\n" +
                $"Selected address:\n{_selectedDerivedRow.AddressXdc}\n\n" +
                "Do you want to continue?",
                "Export Private Key",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            var win = new ExportPrivateKeyWindow
            {
                Owner = this
            };

            win.SetPrivateKeyContext(
                _selectedDerivedRow.AddressXdc,
                Ensure0x(_selectedDerivedRow.PrivateKeyHex0x));

            win.ShowDialog();
        }

        private void CreateKeystoreSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            if (_selectedDerivedRow == null)
            {
                MessageBox.Show(
                    "Please select an address first.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!ValidateBip39PassphraseIfEnabled())
                return;

            var mnemonic = BuildMnemonicFromInputs(out var wordCountOk);
            if (!wordCountOk)
            {
                MessageBox.Show(
                    "Please enter a complete 12 or 24 word recovery phrase.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                "You are about to open the keystore creation window for the selected derived address.\n\n" +
                $"Selected address:\n{_selectedDerivedRow.AddressXdc}\n\n" +
                "Do you want to continue?",
                "Create keystore file",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            var win = new GenerateKeystoreFileWindow
            {
                Owner = this
            };

            win.SetWalletContext(
                _selectedDerivedRow.AddressXdc,
                _selectedDerivedRow.Address0x,
                _selectedDerivedRow.PrivateKeyHex0x,
                _selectedDerivedRow.DerivationPathUsed,
                UsePassphraseCheckBox?.IsChecked == true);

            win.SetRecoveryPhraseContext(
                mnemonic,
                UsePassphraseCheckBox?.IsChecked == true);

            win.ShowDialog();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Authorise_Click(object sender, RoutedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            var selectedIndex = AuthTabControl?.SelectedIndex ?? 0;

            if (selectedIndex == 1)
            {
                AuthoriseFromRecoveryPhrase();
                return;
            }

            if (selectedIndex == 2)
            {
                AuthoriseFromPrivateKey();
                return;
            }

            AuthoriseFromKeystore();
        }

        private void UpdateAuthoriseEnabled()
        {
            if (!_isWindowReady)
                return;

            try
            {
                if (AuthoriseButton == null || AuthTabControl == null)
                    return;

                var selectedIndex = AuthTabControl.SelectedIndex;

                if (selectedIndex == 0)
                {
                    var path = (KeystorePathTextBox?.Text ?? string.Empty).Trim();
                    var fileOk = !string.IsNullOrWhiteSpace(path) && File.Exists(path);

                    var password = (ShowPasswordCheckBox?.IsChecked == true)
                        ? (KeystorePasswordTextBox?.Text ?? string.Empty)
                        : (KeystorePasswordBox?.Password ?? string.Empty);

                    AuthoriseButton.IsEnabled = fileOk && !string.IsNullOrEmpty(password);
                    return;
                }

                if (selectedIndex == 1)
                {
                    var mnemonic = BuildMnemonicFromInputs(out var wordCountOk);
                    var hasMnemonic = wordCountOk && !string.IsNullOrWhiteSpace(mnemonic);
                    var hasSelection = _selectedDerivedRow != null;
                    var bip39Ok = UsePassphraseCheckBox?.IsChecked != true || !string.IsNullOrWhiteSpace(GetPassphraseOrEmpty());

                    AuthoriseButton.IsEnabled = hasMnemonic && hasSelection && bip39Ok;

                    if (CreateKeystoreSelectedButton != null)
                        CreateKeystoreSelectedButton.IsEnabled = hasSelection && bip39Ok;

                    if (CopySelectedAddressButton != null)
                        CopySelectedAddressButton.IsEnabled = hasSelection;

                    if (ExportPrivateKeySelectedButton != null)
                        ExportPrivateKeySelectedButton.IsEnabled = hasSelection;

                    return;
                }

                var hasValidatedPrivateKey =
                    !string.IsNullOrWhiteSpace(_privateKeyValidatedHex0x) &&
                    !string.IsNullOrWhiteSpace(_privateKeyValidatedAddress0x) &&
                    !string.IsNullOrWhiteSpace(_privateKeyValidatedAddressXdc);

                AuthoriseButton.IsEnabled = hasValidatedPrivateKey;

                if (CopyPrivateKeyAddressButton != null)
                    CopyPrivateKeyAddressButton.IsEnabled = hasValidatedPrivateKey;

                if (CreateKeystoreFromPrivateKeyButton != null)
                    CreateKeystoreFromPrivateKeyButton.IsEnabled = hasValidatedPrivateKey;
            }
            catch
            {
                if (AuthoriseButton != null)
                    AuthoriseButton.IsEnabled = false;

                if (CreateKeystoreSelectedButton != null)
                    CreateKeystoreSelectedButton.IsEnabled = false;

                if (CopySelectedAddressButton != null)
                    CopySelectedAddressButton.IsEnabled = false;

                if (ExportPrivateKeySelectedButton != null)
                    ExportPrivateKeySelectedButton.IsEnabled = false;

                if (CopyPrivateKeyAddressButton != null)
                    CopyPrivateKeyAddressButton.IsEnabled = false;

                if (CreateKeystoreFromPrivateKeyButton != null)
                    CreateKeystoreFromPrivateKeyButton.IsEnabled = false;
            }
        }

        private void AuthoriseFromRecoveryPhrase()
        {
            var mnemonic = BuildMnemonicFromInputs(out var wordCountOk);
            if (!wordCountOk)
            {
                MessageBox.Show(
                    "Please enter a complete 12 or 24 word recovery phrase.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!ValidateBip39PassphraseIfEnabled())
                return;

            if (_selectedDerivedRow == null)
            {
                MessageBox.Show(
                    "Please scan and select an address before authorising.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                string privateKeyHex0x;
                string fromAddress0x;

                if (_selectedDerivedRow != null)
                {
                    privateKeyHex0x = Ensure0x(_selectedDerivedRow.PrivateKeyHex0x);
                    fromAddress0x = _selectedDerivedRow.Address0x;
                }
                else
                {
                    var acct0 = WalletDerivationService.DeriveFromMnemonic(
                        mnemonicPhrase: mnemonic,
                        bip39Passphrase: GetPassphraseOrEmpty(),
                        derivationPathTemplate: GetSelectedDerivationPathTemplate(),
                        index: 0);

                    privateKeyHex0x = acct0.PrivateKeyHex0x;
                    fromAddress0x = acct0.Address0x;
                }

                AppSession.Instance.SetWallet(privateKeyHex0x, fromAddress0x);

                foreach (var w in _wordEntries)
                    w.Text = string.Empty;

                SyncWordBoxesToModel();

                if (PassphrasePasswordBox != null) PassphrasePasswordBox.Password = string.Empty;
                if (PassphraseTextBox != null) PassphraseTextBox.Text = string.Empty;
                if (UsePassphraseCheckBox != null) UsePassphraseCheckBox.IsChecked = false;
                if (ShowPhraseCheckBox != null) ShowPhraseCheckBox.IsChecked = false;

                ClearDerivedSelectionAndRows();
                UpdateAuthoriseEnabled();

                DialogResult = true;
                Close();
            }
            catch
            {
                MessageBox.Show(
                    "Unable to authorise from recovery phrase. Please check inputs and try again.",
                    "Authorisation Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void AuthoriseFromKeystore()
        {
            var path = (KeystorePathTextBox?.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show(
                    "Please select a keystore JSON file.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!File.Exists(path))
            {
                MessageBox.Show(
                    "Keystore file not found.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var password = (ShowPasswordCheckBox?.IsChecked == true)
                ? (KeystorePasswordTextBox?.Text ?? string.Empty)
                : (KeystorePasswordBox?.Password ?? string.Empty);

            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show(
                    "Password is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                var json = File.ReadAllText(path);

                try
                {
                    using var _ = JsonDocument.Parse(json);
                }
                catch
                {
                    MessageBox.Show(
                        "Invalid keystore file. Please select a valid keystore JSON.",
                        "Authorisation Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                var keyStore = new KeyStoreService();
                var privateKeyBytes = keyStore.DecryptKeyStoreFromJson(password, json);

                if (privateKeyBytes == null || privateKeyBytes.Length == 0)
                    throw new Exception("Keystore decrypt returned an empty key.");

                var privateKeyHex = "0x" + BitConverter.ToString(privateKeyBytes)
                    .Replace("-", "")
                    .ToLowerInvariant();

                var ecKey = new EthECKey(privateKeyBytes, true);
                var fromAddress0x = ecKey.GetPublicAddress();

                Array.Clear(privateKeyBytes, 0, privateKeyBytes.Length);

                AppSession.Instance.SetWallet(privateKeyHex, fromAddress0x);

                if (KeystorePasswordBox != null) KeystorePasswordBox.Password = string.Empty;
                if (KeystorePasswordTextBox != null) KeystorePasswordTextBox.Text = string.Empty;
                if (ShowPasswordCheckBox != null) ShowPasswordCheckBox.IsChecked = false;

                UpdateAuthoriseEnabled();

                DialogResult = true;
                Close();
            }
            catch
            {
                MessageBox.Show(
                    "Unable to decrypt keystore. Check your password and try again.",
                    "Authorisation Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
        }

        private static string Ensure0x(string pk)
        {
            if (string.IsNullOrWhiteSpace(pk))
                return pk;

            return pk.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? pk : "0x" + pk;
        }

        private sealed class WordEntry
        {
            public WordEntry(int index, bool isPlaceholder, string displayIndex)
            {
                Index = index;
                IsPlaceholder = isPlaceholder;
                DisplayIndex = displayIndex;
            }

            public int Index { get; }
            public bool IsPlaceholder { get; }
            public string DisplayIndex { get; }
            public string Text { get; set; } = string.Empty;
        }

        private sealed class DerivedAddressRow
        {
            public int Index { get; set; }
            public string Address0x { get; set; } = string.Empty;
            public string AddressXdc { get; set; } = string.Empty;
            public string BalanceXdc { get; set; } = "0";
            public string PrivateKeyHex0x { get; set; } = string.Empty;
            public string DerivationPathUsed { get; set; } = string.Empty;
        }
    }
}