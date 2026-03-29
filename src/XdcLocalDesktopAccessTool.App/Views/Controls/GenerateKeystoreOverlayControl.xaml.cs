using Microsoft.Win32;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.KeyStore;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using XdcLocalDesktopAccessTool.App.Services;
using XdcLocalDesktopAccessTool.App;

namespace XdcLocalDesktopAccessTool.App.Views.Controls
{
    public partial class GenerateKeystoreOverlayControl : UserControl
    {
        private readonly ObservableCollection<KeystoreWordEntry> _words = new();

        private string _bip39Passphrase = string.Empty;
        private string _keystorePassword = string.Empty;
        private string _confirmKeystorePassword = string.Empty;

        private string _selectedAddressXdc = string.Empty;
        private string _selectedAddress0x = string.Empty;
        private string _selectedPrivateKeyHex0x = string.Empty;
        private string _selectedDerivationPath = string.Empty;
        private bool _selectedAddressUsesBip39Passphrase;
        private bool _hasWalletContext;
        private bool _recoveryInputsChangedSinceContextSet;
private bool _isSelectedAddressMode;

        private bool _suppressPhraseLengthEvent;
        private bool _windowLoaded;
        private bool _hasPendingRecoveryPhraseContext;
        private string _pendingMnemonicPhrase = string.Empty;
        private bool _pendingUsesBip39Passphrase;

        public GenerateKeystoreOverlayControl()
        {
            InitializeComponent();

            if (WordsItemsControl != null)
                WordsItemsControl.ItemsSource = _words;

            if (ShowPhraseCheckBox != null)
                ShowPhraseCheckBox.IsChecked = false;

            if (UsePassphraseCheckBox != null)
                UsePassphraseCheckBox.IsChecked = false;

            if (PassphraseRowGrid != null)
                PassphraseRowGrid.Visibility = Visibility.Hidden;

            if (ShowPassphraseCheckBox != null)
            {
                ShowPassphraseCheckBox.IsEnabled = false;
                ShowPassphraseCheckBox.IsChecked = false;
            }

            if (PassphrasePasswordBox != null)
                PassphrasePasswordBox.Visibility = Visibility.Visible;

            if (PassphrasePlainTextBox != null)
                PassphrasePlainTextBox.Visibility = Visibility.Collapsed;

            SetKeystorePasswordsVisibility(false);
            InitialiseWords(12);
            UpdateSelectedAddressHeader();
            UpdateOutputDetails(null);
            UpdateCreateKeystoreButtonState();

            Loaded += GenerateKeystoreOverlayControl_Loaded;
        }

        private void GenerateKeystoreOverlayControl_Loaded(object sender, RoutedEventArgs e)
        {
            _windowLoaded = true;

            Dispatcher.BeginInvoke(
                new Action(ApplyPendingRecoveryPhraseContextIfAny),
                DispatcherPriority.ContextIdle);
        }

        public void SetWalletContext(
            string addressXdc,
            string address0x,
            string privateKeyHex0x,
            string derivationPathUsed,
            bool usedBip39Passphrase)
        {
            _selectedAddressXdc = addressXdc ?? string.Empty;
            _selectedAddress0x = address0x ?? string.Empty;
            _selectedPrivateKeyHex0x = privateKeyHex0x ?? string.Empty;
            _selectedDerivationPath = derivationPathUsed ?? string.Empty;
            _selectedAddressUsesBip39Passphrase = usedBip39Passphrase;
            _hasWalletContext = !string.IsNullOrWhiteSpace(_selectedPrivateKeyHex0x);
_isSelectedAddressMode = _hasWalletContext;

            ApplyDerivationPathToCombo(_selectedDerivationPath);
            UpdateSelectedAddressHeader();
            UpdateOutputDetails(null);
            UpdateCreateKeystoreButtonState();

            // Reset after context has been fully applied so setup events
            // do not falsely count as user recovery-input changes.
            _recoveryInputsChangedSinceContextSet = false;
        }

        public void SetRecoveryPhraseContext(string mnemonicPhrase, bool usesBip39Passphrase)
        {
            _pendingMnemonicPhrase = (mnemonicPhrase ?? string.Empty).Trim();
            _pendingUsesBip39Passphrase = usesBip39Passphrase;
            _hasPendingRecoveryPhraseContext = !string.IsNullOrWhiteSpace(_pendingMnemonicPhrase);

            if (_windowLoaded)
            {
                Dispatcher.BeginInvoke(
                    new Action(ApplyPendingRecoveryPhraseContextIfAny),
                    DispatcherPriority.ContextIdle);
            }
        }

        private void ApplyPendingRecoveryPhraseContextIfAny()
        {
            if (!_hasPendingRecoveryPhraseContext)
                return;

            _hasPendingRecoveryPhraseContext = false;

            var phraseWords = _pendingMnemonicPhrase
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim().ToLowerInvariant())
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .ToArray();

            _suppressPhraseLengthEvent = true;

            if (UsePassphraseCheckBox != null)
                UsePassphraseCheckBox.IsChecked = _pendingUsesBip39Passphrase;

            if (phraseWords.Length == 24)
            {
                if (PhraseLengthComboBox != null)
                    PhraseLengthComboBox.SelectedIndex = 1;

                InitialiseWords(24);
            }
            else
            {
                if (PhraseLengthComboBox != null)
                    PhraseLengthComboBox.SelectedIndex = 0;

                InitialiseWords(12);
            }

            int wordCursor = 0;
            for (int i = 0; i < _words.Count && wordCursor < phraseWords.Length; i++)
            {
                if (_words[i].IsPlaceholder)
                    continue;

                _words[i].Text = phraseWords[wordCursor];
                wordCursor++;
            }

            _suppressPhraseLengthEvent = false;

            UpdateLayout();
            WordsItemsControl?.UpdateLayout();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateLayout();
                WordsItemsControl?.UpdateLayout();
                SyncWordBoxesToModel();
                ApplyMaskingFromCheckbox();
                UpdateOutputDetails(null);
                UpdateCreateKeystoreButtonState();
ApplySelectedAddressModeLock();
}), DispatcherPriority.ApplicationIdle);
        }

        private void ApplyDerivationPathToCombo(string derivationPathUsed)
        {
            if (DerivationPathComboBox == null || string.IsNullOrWhiteSpace(derivationPathUsed))
                return;

            var normalised = derivationPathUsed.Trim().Replace("\\", "/");

            if (normalised.StartsWith("m/44'/60'/0'/0/", StringComparison.OrdinalIgnoreCase))
            {
                DerivationPathComboBox.SelectedIndex = 1;
            }
            else if (normalised.StartsWith("m/44'/550'/0'/0/", StringComparison.OrdinalIgnoreCase))
            {
                DerivationPathComboBox.SelectedIndex = 0;
            }
        }

        private void InitialiseWords(int count)
        {
            _words.Clear();

            if (count == 12)
            {
                int wordNumber = 1;

                for (int i = 0; i < 24; i++)
                {
                    bool isSpacerRow =
                        (i >= 4 && i <= 7) ||
                        (i >= 12 && i <= 15) ||
                        (i >= 20 && i <= 23);

                    if (isSpacerRow)
                    {
                        _words.Add(new KeystoreWordEntry(i, true, string.Empty));
                        continue;
                    }

                    _words.Add(new KeystoreWordEntry(i, false, wordNumber.ToString(CultureInfo.InvariantCulture)));
                    wordNumber++;
                }
            }
            else
            {
                for (int i = 0; i < 24; i++)
                    _words.Add(new KeystoreWordEntry(i, false, (i + 1).ToString(CultureInfo.InvariantCulture)));
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                SyncWordBoxesToModel();
                ApplyMaskingFromCheckbox();
                UpdateCreateKeystoreButtonState();
            }), DispatcherPriority.ContextIdle);
        }

        private void PhraseLengthComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPhraseLengthEvent)
                return;


            MarkRecoveryInputsChanged();
            bool want24 = PhraseLengthComboBox != null && PhraseLengthComboBox.SelectedIndex == 1;
            InitialiseWords(want24 ? 24 : 12);
            UpdateOutputDetails(null);
            UpdateCreateKeystoreButtonState();
        }

        private void DerivationPathComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MarkRecoveryInputsChanged();
            UpdateOutputDetails(null);
            UpdateCreateKeystoreButtonState();
        }

        private void ShowPhraseCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            SyncWordBoxesToModel();
            ApplyMaskingFromCheckbox();
        }

        private void UsePassphraseCheckBox_Changed(object sender, RoutedEventArgs e)
        {

            bool enabled = UsePassphraseCheckBox?.IsChecked == true;

            if (PassphraseRowGrid != null)
                PassphraseRowGrid.Visibility = enabled ? Visibility.Visible : Visibility.Hidden;

            if (ShowPassphraseCheckBox != null)
            {
                ShowPassphraseCheckBox.IsEnabled = enabled;
                if (!enabled)
                    ShowPassphraseCheckBox.IsChecked = false;
            }

            if (enabled)
            {
                MessageBox.Show(
                    "The BIP39 passphrase acts as a second password.\n\n" +
                    "If a different passphrase is entered, a completely different wallet will be generated.\n\n" +
                    "Make sure you enter the exact passphrase originally used for this wallet.",
                    "BIP39 Passphrase",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            if (!enabled)
            {
                _bip39Passphrase = string.Empty;

                if (PassphrasePasswordBox != null)
                    PassphrasePasswordBox.Password = string.Empty;

                if (PassphrasePlainTextBox != null)
                    PassphrasePlainTextBox.Text = string.Empty;

                SwapPassphraseVisibility(false);
            }

            UpdateOutputDetails(null);
            UpdateCreateKeystoreButtonState();
        }

        private void ShowPassphraseCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            SwapPassphraseVisibility(true);
        }

        private void ShowPassphraseCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            SwapPassphraseVisibility(false);
        }

        private void SwapPassphraseVisibility(bool showPlainText)
        {
            if (PassphrasePasswordBox == null || PassphrasePlainTextBox == null)
                return;

            if (showPlainText)
            {
                PassphrasePlainTextBox.Text = PassphrasePasswordBox.Password;
                PassphrasePlainTextBox.Visibility = Visibility.Visible;
                PassphrasePasswordBox.Visibility = Visibility.Collapsed;
                PassphrasePlainTextBox.Focus();
                PassphrasePlainTextBox.CaretIndex = PassphrasePlainTextBox.Text.Length;
            }
            else
            {
                PassphrasePasswordBox.Password = PassphrasePlainTextBox.Text ?? string.Empty;
                PassphrasePasswordBox.Visibility = Visibility.Visible;
                PassphrasePlainTextBox.Visibility = Visibility.Collapsed;
            }

            _bip39Passphrase = PassphrasePasswordBox.Visibility == Visibility.Visible
                ? PassphrasePasswordBox.Password ?? string.Empty
                : PassphrasePlainTextBox.Text ?? string.Empty;
        }

        private void PassphrasePasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {

            if (PassphrasePasswordBox != null && PassphrasePasswordBox.Visibility == Visibility.Visible)
                _bip39Passphrase = PassphrasePasswordBox.Password ?? string.Empty;

            UpdateOutputDetails(null);
            UpdateCreateKeystoreButtonState();
        }

        private void PassphrasePlainTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

            if (PassphrasePlainTextBox != null && PassphrasePlainTextBox.Visibility == Visibility.Visible)
                _bip39Passphrase = PassphrasePlainTextBox.Text ?? string.Empty;

            UpdateOutputDetails(null);
            UpdateCreateKeystoreButtonState();
        }

        private void ShowKeystorePasswordsCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            SetKeystorePasswordsVisibility(true);
            UpdateCreateKeystoreButtonState();
        }

        private void ShowKeystorePasswordsCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            SetKeystorePasswordsVisibility(false);
            UpdateCreateKeystoreButtonState();
        }

        private void SetKeystorePasswordsVisibility(bool showPlainText)
        {
            if (KeystorePasswordBox == null ||
                KeystorePasswordPlainTextBox == null ||
                ConfirmKeystorePasswordBox == null ||
                ConfirmKeystorePasswordPlainTextBox == null)
                return;

            if (showPlainText)
            {
                KeystorePasswordPlainTextBox.Text = KeystorePasswordBox.Password;
                ConfirmKeystorePasswordPlainTextBox.Text = ConfirmKeystorePasswordBox.Password;

                KeystorePasswordPlainTextBox.Visibility = Visibility.Visible;
                ConfirmKeystorePasswordPlainTextBox.Visibility = Visibility.Visible;

                KeystorePasswordBox.Visibility = Visibility.Collapsed;
                ConfirmKeystorePasswordBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                KeystorePasswordBox.Password = KeystorePasswordPlainTextBox.Text ?? string.Empty;
                ConfirmKeystorePasswordBox.Password = ConfirmKeystorePasswordPlainTextBox.Text ?? string.Empty;

                KeystorePasswordBox.Visibility = Visibility.Visible;
                ConfirmKeystorePasswordBox.Visibility = Visibility.Visible;

                KeystorePasswordPlainTextBox.Visibility = Visibility.Collapsed;
                ConfirmKeystorePasswordPlainTextBox.Visibility = Visibility.Collapsed;
            }

            _keystorePassword = KeystorePasswordBox.Visibility == Visibility.Visible
                ? KeystorePasswordBox.Password ?? string.Empty
                : KeystorePasswordPlainTextBox.Text ?? string.Empty;

            _confirmKeystorePassword = ConfirmKeystorePasswordBox.Visibility == Visibility.Visible
                ? ConfirmKeystorePasswordBox.Password ?? string.Empty
                : ConfirmKeystorePasswordPlainTextBox.Text ?? string.Empty;
        }

        private void KeystorePasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {

            if (KeystorePasswordBox != null && KeystorePasswordBox.Visibility == Visibility.Visible)
                _keystorePassword = KeystorePasswordBox.Password ?? string.Empty;

            UpdateCreateKeystoreButtonState();
        }

        private void KeystorePasswordPlainTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

            if (KeystorePasswordPlainTextBox != null && KeystorePasswordPlainTextBox.Visibility == Visibility.Visible)
                _keystorePassword = KeystorePasswordPlainTextBox.Text ?? string.Empty;

            UpdateCreateKeystoreButtonState();
        }

        private void ConfirmKeystorePasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {

            if (ConfirmKeystorePasswordBox != null && ConfirmKeystorePasswordBox.Visibility == Visibility.Visible)
                _confirmKeystorePassword = ConfirmKeystorePasswordBox.Password ?? string.Empty;

            UpdateCreateKeystoreButtonState();
        }

        private void ConfirmKeystorePasswordPlainTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

            if (ConfirmKeystorePasswordPlainTextBox != null && ConfirmKeystorePasswordPlainTextBox.Visibility == Visibility.Visible)
                _confirmKeystorePassword = ConfirmKeystorePasswordPlainTextBox.Text ?? string.Empty;

            UpdateCreateKeystoreButtonState();
        }

        private void BrowseKeystorePathButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new SaveFileDialog
                {
                    Title = "Save keystore file",
                    Filter = "Keystore JSON (*.json)|*.json|All files (*.*)|*.*",
                    DefaultExt = ".json",
                    AddExtension = true,
                    FileName = "xdc-keystore.json",
                    OverwritePrompt = true
                };

                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrWhiteSpace(docs) && Directory.Exists(docs))
                    dlg.InitialDirectory = docs;

                var ownerWindow = Window.GetWindow(this);
                bool ok = ownerWindow != null ? dlg.ShowDialog(ownerWindow) == true : dlg.ShowDialog() == true;
                if (!ok)
                    return;

                if (KeystorePathTextBox != null)
                {
                    KeystorePathTextBox.Text = dlg.FileName;
                    KeystorePathTextBox.Foreground = Brushes.Black;
                }

                    UpdateOutputDetails(dlg.FileName);
                UpdateCreateKeystoreButtonState();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to select a save location.\n\n" + ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UpdateCreateKeystoreButtonState()
        {
            if (CreateKeystoreButton == null)
                return;

            bool wordCountOk;
            string mnemonic = BuildMnemonicFromInputs(out wordCountOk);

            bool hasCompletePhrase = wordCountOk && !string.IsNullOrWhiteSpace(mnemonic);
            bool hasKeystorePassword = !string.IsNullOrWhiteSpace(_keystorePassword);
            bool hasConfirmPassword = !string.IsNullOrWhiteSpace(_confirmKeystorePassword);
            bool passwordsMatch = string.Equals(_keystorePassword, _confirmKeystorePassword, StringComparison.Ordinal);

            bool usePassphrase = UsePassphraseCheckBox?.IsChecked == true;
            bool hasRequiredPassphrase = !usePassphrase || !string.IsNullOrWhiteSpace(_bip39Passphrase);

            string savePath = KeystorePathTextBox?.Text ?? string.Empty;
            bool hasSavePath =
                !string.IsNullOrWhiteSpace(savePath) &&
                !string.Equals(savePath, "Select a save location...", StringComparison.OrdinalIgnoreCase);

            CreateKeystoreButton.IsEnabled =
                hasCompletePhrase &&
                hasKeystorePassword &&
                hasConfirmPassword &&
                passwordsMatch &&
                hasRequiredPassphrase &&
                hasSavePath;
        }

        private void UpdateSelectedAddressHeader()
        {
            if (SelectedAddressHeaderTextBlock == null)
                return;

            if (_hasWalletContext && !string.IsNullOrWhiteSpace(_selectedAddressXdc))
                SelectedAddressHeaderTextBlock.Text = $"Selected address: {_selectedAddressXdc}";
            else
                SelectedAddressHeaderTextBlock.Text = "Selected address: Derived on create (index 0)";
        }

        private void UpdateOutputDetails(string? savePathOverride)
        {
            if (OutputDetailsTextBox == null)
                return;

            string savePath = savePathOverride ?? KeystorePathTextBox?.Text ?? string.Empty;
            bool hasPassphrase = UsePassphraseCheckBox?.IsChecked == true;

            string addressText = _hasWalletContext && !string.IsNullOrWhiteSpace(_selectedAddressXdc)
                ? _selectedAddressXdc
                : "Derived on create (index 0)";

            string derivationText = _hasWalletContext && !string.IsNullOrWhiteSpace(_selectedDerivationPath)
                ? _selectedDerivationPath
                : GetSelectedDerivationPathForIndex0();

            string sourceText = _hasWalletContext
                ? "Selected derived address"
                : "Seed phrase (will derive default index 0 on create)";

            string text =
                "Keystore file details\n\n" +
                $"Source: {sourceText}\n" +
                $"Address: {addressText}\n" +
                $"Derivation path: {derivationText}\n" +
                $"BIP39 passphrase: {(hasPassphrase || _selectedAddressUsesBip39Passphrase ? "Used" : "Not used")}";

            if (!string.IsNullOrWhiteSpace(savePath) &&
                !string.Equals(savePath, "Select a save location...", StringComparison.OrdinalIgnoreCase))
            {
                text += $"\n\nSave location: {savePath}";
            }
            else
            {
                text += "\n\nSave location: Select a save location...";
            }

            OutputDetailsTextBox.Text = text;
        }

        private void ApplyMaskingFromCheckbox()
        {
            try
            {
                if (WordsItemsControl == null)
                    return;

                bool alwaysShow = ShowPhraseCheckBox?.IsChecked == true;

                for (int i = 0; i < _words.Count; i++)
                {
                    if (_words[i].IsPlaceholder)
                        continue;

                    var container = WordsItemsControl.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                    if (container == null)
                        continue;

                    var cell = FindChild<Border>(container, "WordCellRoot");
                    if (cell == null)
                        continue;

                    SetWordCellMasked(cell, !alwaysShow);
                }
            }
            catch
            {
            }
        }

        private void WordPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {

            if (sender is not PasswordBox pb)
                return;

            if (pb.DataContext is not KeystoreWordEntry entry)
                return;

            if (entry.IsPlaceholder)
                return;

            entry.Text = pb.Password ?? string.Empty;
            UpdateCreateKeystoreButtonState();
        }

        private void WordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

            if (sender is not TextBox tb)
                return;

            if (tb.DataContext is not KeystoreWordEntry entry)
                return;

            if (entry.IsPlaceholder)
                return;

            entry.Text = tb.Text ?? string.Empty;
            UpdateCreateKeystoreButtonState();
        }

        private void WordBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (ShowPhraseCheckBox?.IsChecked == true)
                return;

            var cell = FindWordCellRoot(sender as DependencyObject);
            if (cell == null)
                return;

            if (cell.DataContext is KeystoreWordEntry entry && entry.IsPlaceholder)
                return;

            SetWordCellMasked(cell, false);

            if (sender is PasswordBox)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var tb = FindChild<TextBox>(cell, "WordTextBox");
                    if (tb == null)
                        return;

                    tb.Focus();
                    tb.CaretIndex = tb.Text?.Length ?? 0;
                }));
            }
        }

        private void WordBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (ShowPhraseCheckBox?.IsChecked == true)
                return;

            var cell = FindWordCellRoot(sender as DependencyObject);
            if (cell == null)
                return;

            if (cell.DataContext is KeystoreWordEntry entry && entry.IsPlaceholder)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                var focused = Keyboard.FocusedElement as DependencyObject;
                if (focused != null && IsDescendant(cell, focused))
                    return;

                SetWordCellMasked(cell, true);
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
            if (cellRoot.DataContext is KeystoreWordEntry entry && entry.IsPlaceholder)
                return;

            var tb = FindChild<TextBox>(cellRoot, "WordTextBox");
            var pb = FindChild<PasswordBox>(cellRoot, "WordPasswordBox");
            if (tb == null || pb == null)
                return;

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

                for (int i = 0; i < _words.Count; i++)
                {
                    if (_words[i].IsPlaceholder)
                        continue;

                    var container = WordsItemsControl.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                    if (container == null)
                        continue;

                    var cell = FindChild<Border>(container, "WordCellRoot");
                    if (cell == null)
                        continue;

                    var pb = FindChild<PasswordBox>(cell, "WordPasswordBox");
                    var tb = FindChild<TextBox>(cell, "WordTextBox");

                    string text = _words[i].Text ?? string.Empty;

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
            int count = VisualTreeHelper.GetChildrenCount(parent);
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

        private void ApplySelectedAddressModeLock()
{
    if (!_isSelectedAddressMode)
        return;

    if (PhraseLengthComboBox != null)
        PhraseLengthComboBox.IsEnabled = false;

    if (DerivationPathComboBox != null)
        DerivationPathComboBox.IsEnabled = false;

    if (ShowPhraseCheckBox != null)
        ShowPhraseCheckBox.IsEnabled = false;

    if (UsePassphraseCheckBox != null)
        UsePassphraseCheckBox.IsEnabled = false;

    if (ShowPassphraseCheckBox != null)
        ShowPassphraseCheckBox.IsEnabled = false;

    if (PassphrasePasswordBox != null)
        PassphrasePasswordBox.IsEnabled = false;

    if (PassphrasePlainTextBox != null)
        PassphrasePlainTextBox.IsEnabled = false;

    // Lock seed phrase boxes (visual + interaction)
    if (WordsItemsControl != null)
        WordsItemsControl.IsEnabled = false;
}

private void CopyAddressButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_selectedAddressXdc))
                return;

            try
            {
                Clipboard.SetText(_selectedAddressXdc);
                MessageBox.Show(
                    "Address copied to clipboard.",
                    "Copy address",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to copy address.\n\n" + ex.Message,
                    "Copy address",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            CloseOverlay();
        }

        private void CreateKeystore_Click(object sender, RoutedEventArgs e)
        {
            string mnemonic = BuildMnemonicFromInputs(out var wordCountOk);

            if (!wordCountOk || string.IsNullOrWhiteSpace(mnemonic))
            {
                MessageBox.Show(
                    "Please enter a complete 12 or 24 word seed phrase.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                _ = new NBitcoin.Mnemonic(mnemonic);
            }
            catch
            {
                MessageBox.Show(
                    "Invalid seed phrase. Check the spelling and order of the recovery words.",
                    "Seed phrase error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(_keystorePassword))
            {
                MessageBox.Show(
                    "Please enter a new keystore password.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(_confirmKeystorePassword))
            {
                MessageBox.Show(
                    "Please confirm the new keystore password.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!string.Equals(_keystorePassword, _confirmKeystorePassword, StringComparison.Ordinal))
            {
                MessageBox.Show(
                    "The keystore passwords do not match.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (_keystorePassword.Trim().Length < 8)
            {
                MessageBox.Show(
                    "Keystore password must be at least 8 characters long.\n\nLonger, stronger passwords are strongly recommended.",
                    "Weak Password",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string savePath = KeystorePathTextBox?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(savePath) ||
                string.Equals(savePath, "Select a save location...", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "Please choose a save location.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (_hasWalletContext && _recoveryInputsChangedSinceContextSet)
            {
                MessageBox.Show(
                    "You changed the seed phrase, BIP39 passphrase, or derivation path after selecting an address.\n\n" +
                    "Please go back, rescan addresses, and reselect the correct address before continuing.",
                    "Recovery inputs changed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (_hasWalletContext && !CurrentInputsMatchSelectedWallet(mnemonic))
            {
                MessageBox.Show(
                    "The current recovery phrase settings no longer match the selected address.\n\n" +
                    "Continuing would create a keystore for a different wallet.\n\n" +
                    "Please go back, rescan addresses, and reselect the correct address before continuing.",
                    "Selected address mismatch",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string privateKeyHex0x = _selectedPrivateKeyHex0x;
            string address0x = _selectedAddress0x;
            string addressXdc = _selectedAddressXdc;
            string derivationPathUsed = _selectedDerivationPath;

            if (!_hasWalletContext || string.IsNullOrWhiteSpace(privateKeyHex0x))
            {
                try
                {
                    var acct0 = WalletDerivationService.DeriveFromMnemonic(
                        mnemonicPhrase: mnemonic,
                        bip39Passphrase: GetPassphraseOrEmpty(),
                        derivationPathTemplate: GetSelectedDerivationPathTemplate(),
                        index: 0);

                    privateKeyHex0x = acct0.PrivateKeyHex0x;
                    address0x = acct0.Address0x;
                    addressXdc = acct0.AddressXdc;
                    derivationPathUsed = acct0.DerivationPathUsed;

                    _selectedPrivateKeyHex0x = privateKeyHex0x;
                    _selectedAddress0x = address0x;
                    _selectedAddressXdc = addressXdc;
                    _selectedDerivationPath = derivationPathUsed;
                    _hasWalletContext = true;

                    UpdateSelectedAddressHeader();
                    UpdateOutputDetails(null);
                }
                catch (Exception)
                {
                    MessageBox.Show(
                        "Unable to derive a wallet from the seed phrase.\n\n" +
                        "Please check that:\n" +
                        "• each word is spelled correctly\n" +
                        "• the words are in the correct order\n" +
                        "• the phrase length is correct\n" +
                        "• the BIP39 passphrase is correct (if one was used)",
                        "Seed phrase error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }

            try
            {
                byte[] privateKeyBytes = privateKeyHex0x.HexToByteArray();

                var keyStoreService = new KeyStoreService();
                string json = keyStoreService.EncryptAndGenerateDefaultKeyStoreAsJson(
                    _keystorePassword,
                    privateKeyBytes,
                    address0x);

                File.WriteAllText(savePath, json);

                Array.Clear(privateKeyBytes, 0, privateKeyBytes.Length);

                _selectedAddress0x = address0x;
                _selectedAddressXdc = addressXdc;
                _selectedDerivationPath = derivationPathUsed;
                _hasWalletContext = true;

                UpdateSelectedAddressHeader();
                UpdateOutputDetails(savePath);

                MessageBox.Show(
                    "Keystore file created successfully.\n\n" +
                    $"Address:\n{addressXdc}\n\n" +
                    $"Saved to:\n{savePath}",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to create keystore.\n\n" + ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private string BuildMnemonicFromInputs(out bool wordCountOk)
        {
            var words = _words
                .Where(w => !w.IsPlaceholder)
                .Select(w => (w.Text ?? string.Empty).Trim().ToLowerInvariant())
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .ToArray();

            int expected = (PhraseLengthComboBox != null && PhraseLengthComboBox.SelectedIndex == 1) ? 24 : 12;
            wordCountOk = words.Length == expected;

            return string.Join(' ', words);
        }

        private string GetSelectedDerivationPathTemplate()
        {
            if (DerivationPathComboBox?.SelectedItem is ComboBoxItem item)
            {
                string? tag = item.Tag?.ToString();
                if (!string.IsNullOrWhiteSpace(tag))
                    return tag.Trim();
            }

            return "m/44'/550'/0'/0/{i}";
        }

        private string GetSelectedDerivationPathForIndex0()
        {
            var template = GetSelectedDerivationPathTemplate();
            return template.Replace("{i}", "0", StringComparison.OrdinalIgnoreCase);
        }

        private string GetPassphraseOrEmpty()
        {
            return UsePassphraseCheckBox?.IsChecked == true
                ? (_bip39Passphrase ?? string.Empty)
                : string.Empty;
        }

        private void MarkRecoveryInputsChanged()
        {
            if (_suppressPhraseLengthEvent)
                return;

            if (_hasWalletContext)
                _recoveryInputsChangedSinceContextSet = true;
        }

        private static bool TryParseDerivationIndex(string derivationPath, out int index)
        {
            index = 0;

            if (string.IsNullOrWhiteSpace(derivationPath))
                return false;

            string normalised = derivationPath.Trim().Replace("\\", "/");
            int lastSlash = normalised.LastIndexOf('/');
            if (lastSlash < 0 || lastSlash >= normalised.Length - 1)
                return false;

            return int.TryParse(normalised.Substring(lastSlash + 1), out index);
        }

        private bool CurrentInputsMatchSelectedWallet(string mnemonic)
        {
            if (!_hasWalletContext || string.IsNullOrWhiteSpace(_selectedAddressXdc))
                return true;

            if (!TryParseDerivationIndex(_selectedDerivationPath, out int selectedIndex))
                return false;

            var derived = WalletDerivationService.DeriveFromMnemonic(
                mnemonicPhrase: mnemonic,
                bip39Passphrase: GetPassphraseOrEmpty(),
                derivationPathTemplate: GetSelectedDerivationPathTemplate(),
                index: selectedIndex);

            return string.Equals(
                derived.AddressXdc?.Trim(),
                _selectedAddressXdc?.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private void CloseOverlay()
        {
            var main = Window.GetWindow(this) as MainWindow;
            main?.CloseGenerateKeystoreOverlay();
        }


        private sealed class KeystoreWordEntry
        {
            public KeystoreWordEntry(int index, bool isPlaceholder, string displayIndex)
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
    }
}






















