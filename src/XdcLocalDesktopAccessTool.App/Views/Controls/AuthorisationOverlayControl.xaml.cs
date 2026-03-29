using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Nethereum.KeyStore;
using Nethereum.Signer;
using Nethereum.Web3;
using Nethereum.Util;
using XdcLocalDesktopAccessTool.App.Services;

namespace XdcLocalDesktopAccessTool.App.Views.Controls
{
    public partial class AuthorisationOverlayControl : UserControl
    {
        public bool? Result { get; private set; }
        public string? AuthorisedSeedPhrase { get; private set; }

        private readonly ObservableCollection<WordEntry> _wordEntries = new();
        private readonly ObservableCollection<DerivedSeedAccountItem> _derivedSeedAccounts = new();
        private DerivedSeedAccountItem? _selectedDerivedSeedAccount;

        private bool _isWindowReady;
        private bool _suppressUiEvents;
        private int _lastAuthTabIndex;
        private int _lastPhraseLengthIndex;
        private int _lastDerivationPathIndex;
        private bool _lastUsePassphraseEnabled;

        private bool _seedSelectionInvalidatedNoticeShown;
        private string _privateKeyValidatedHex0x = string.Empty;
        private string _privateKeyValidatedAddress0x = string.Empty;
        private string _privateKeyValidatedAddressXdc = string.Empty;

        public AuthorisationOverlayControl()
        {
            _isWindowReady = false;
            _suppressUiEvents = false;

            InitializeComponent();

            if (WordsItemsControl != null)
                WordsItemsControl.ItemsSource = _wordEntries;

            if (DerivedAddressesDataGrid != null)
                DerivedAddressesDataGrid.ItemsSource = _derivedSeedAccounts;

            CancelButton.Click += CancelButton_Click;
            KeyDown += AuthorisationOverlayControl_KeyDown;
            PreviewKeyDown += Overlay_PreviewKeyDown;

            Loaded += (_, __) =>
            {
                Focus();
                Keyboard.Focus(this);
            };

            InitialiseSeedPhraseUi();
            InitialisePrivateKeyUi();

            _lastAuthTabIndex = AuthTabControl?.SelectedIndex ?? 0;
            _lastPhraseLengthIndex = PhraseLengthComboBox?.SelectedIndex ?? 0;
            _lastDerivationPathIndex = DerivationPathComboBox?.SelectedIndex ?? 0;

            _isWindowReady = true;
            UpdateAuthoriseEnabled();

            RefreshSeedAuthoriseState();
        }

        private void InitialiseSeedPhraseUi()
        {
            if (PhraseLengthComboBox != null) PhraseLengthComboBox.SelectedIndex = 0;

            if (UsePassphraseCheckBox != null) UsePassphraseCheckBox.IsChecked = false;
            if (ShowPhraseCheckBox != null) ShowPhraseCheckBox.IsChecked = false;

            if (PassphraseRowGrid != null) PassphraseRowGrid.Visibility = Visibility.Hidden;
            if (Bip39PassphraseInfoTextBlock != null) Bip39PassphraseInfoTextBlock.Visibility = Visibility.Collapsed;
            if (ShowBip39PassphraseCheckBox != null)
            {
                ShowBip39PassphraseCheckBox.IsChecked = false;
                ShowBip39PassphraseCheckBox.IsEnabled = false;
            }

            if (PassphrasePasswordBox != null) PassphrasePasswordBox.Password = string.Empty;
            if (PassphraseTextBox != null) PassphraseTextBox.Text = string.Empty;

            SwapPassphraseVisibility(showPlainText: false);
            BuildWordEntries(12);

            _lastPhraseLengthIndex = PhraseLengthComboBox?.SelectedIndex ?? 0;
        }
        private void RefreshSeedAuthoriseState()
        {
            if (SeedPhraseTab != null && SeedPhraseTab.IsSelected && AuthoriseButton != null)
            {
                var phraseReady = IsSeedPhraseComplete(out _);
                var hasSelectedAddress = _selectedDerivedSeedAccount != null;
                var inputsStillMatch = hasSelectedAddress &&
                                       SelectedDerivedAddressStillMatchesCurrentInputs(out _);

                AuthoriseButton.IsEnabled = phraseReady && hasSelectedAddress && inputsStillMatch;
            }

            RefreshSeedScanUi();
        }

        private void BrowseKeystore_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select keystore JSON file",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() == true)
            {
                KeystorePathTextBox.Text = dlg.FileName;
                UpdateAuthoriseEnabled();

            RefreshSeedAuthoriseState();
            }
        }

        private void ShowPasswordCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            KeystorePasswordTextBox.Text = KeystorePasswordBox.Password;
            KeystorePasswordBox.Visibility = Visibility.Collapsed;
            KeystorePasswordTextBox.Visibility = Visibility.Visible;
            KeystorePasswordTextBox.Focus();
            KeystorePasswordTextBox.CaretIndex = KeystorePasswordTextBox.Text.Length;
            UpdateAuthoriseEnabled();

            RefreshSeedAuthoriseState();
        }

        private void ShowPasswordCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            KeystorePasswordBox.Password = KeystorePasswordTextBox.Text;
            KeystorePasswordTextBox.Visibility = Visibility.Collapsed;
            KeystorePasswordBox.Visibility = Visibility.Visible;
            KeystorePasswordBox.Focus();
            UpdateAuthoriseEnabled();

            RefreshSeedAuthoriseState();
        }

        private void AuthPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdateAuthoriseEnabled();

            RefreshSeedAuthoriseState();
        }

        private void AuthInputs_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            UpdateAuthoriseEnabled();

            RefreshSeedAuthoriseState();
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

            if (_lastAuthTabIndex == 1)
            {
                canLeave = ConfirmLeaveRecoverySection(
                    "Changing this screen will clear the current seed phrase, BIP39 passphrase, and any scanned or selected addresses.\n\nDo you want to continue?");
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
                RefreshSeedAuthoriseState();
            NotifyRecoveryInputsChangedIfSelectionBecameInvalid();
            }));
        }

        private void PhraseLengthComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents || PhraseLengthComboBox == null)
                return;

            var newIndex = PhraseLengthComboBox.SelectedIndex;
            if (newIndex == _lastPhraseLengthIndex)
            {
                UpdateAuthoriseEnabled();
                RefreshSeedAuthoriseState();
            NotifyRecoveryInputsChangedIfSelectionBecameInvalid();
                return;
            }

            if (!ConfirmLeaveRecoverySection(
                    "Changing phrase length will clear the current seed phrase, BIP39 passphrase, and any scanned or selected addresses.\n\nDo you want to continue?"))
            {
                _suppressUiEvents = true;
                PhraseLengthComboBox.SelectedIndex = _lastPhraseLengthIndex;
                _suppressUiEvents = false;
                UpdateAuthoriseEnabled();
                RefreshSeedAuthoriseState();
            NotifyRecoveryInputsChangedIfSelectionBecameInvalid();
                return;
            }

            var want24 = newIndex == 1;
            BuildWordEntries(want24 ? 24 : 12);

            _lastPhraseLengthIndex = newIndex;
            UpdateAuthoriseEnabled();
            RefreshSeedAuthoriseState();
            NotifyRecoveryInputsChangedIfSelectionBecameInvalid();
        }

        private void RecoveryInputs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            if (!ReferenceEquals(sender, DerivationPathComboBox))
            {
                UpdateAuthoriseEnabled();
                RefreshSeedAuthoriseState();
            NotifyRecoveryInputsChangedIfSelectionBecameInvalid();
                return;
            }

            if (DerivationPathComboBox == null)
                return;

            var newIndex = DerivationPathComboBox.SelectedIndex;
            if (newIndex == _lastDerivationPathIndex)
            {
                UpdateAuthoriseEnabled();
                RefreshSeedAuthoriseState();
            NotifyRecoveryInputsChangedIfSelectionBecameInvalid();
                return;
            }

            _lastDerivationPathIndex = newIndex;

            if (_derivedSeedAccounts.Count > 0 || _selectedDerivedSeedAccount != null)
            {
                ClearDerivedSeedAccounts();

                MessageBox.Show(
                    "The derivation path has changed.\n\nYou will need to click Scan now again to view addresses for the new path.",
                    "Derivation Path Changed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            UpdateAuthoriseEnabled();
            RefreshSeedAuthoriseState();
            NotifyRecoveryInputsChangedIfSelectionBecameInvalid();
        }
        private void UsePassphraseCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            var enabled = UsePassphraseCheckBox != null && UsePassphraseCheckBox.IsChecked == true;
            bool showBip39Warning = enabled && !_lastUsePassphraseEnabled;
            _lastUsePassphraseEnabled = enabled;

            if (PassphraseRowGrid != null)
                PassphraseRowGrid.Visibility = enabled ? Visibility.Visible : Visibility.Hidden;

            if (Bip39PassphraseInfoTextBlock != null)
                Bip39PassphraseInfoTextBlock.Visibility = Visibility.Collapsed;

            if (showBip39Warning)
            {
                MessageBox.Show(
                    "BIP39 passphrase changes the derived wallet addresses. You must use the exact same passphrase again to recover this wallet.",
                    "BIP39 Passphrase Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            if (ShowBip39PassphraseCheckBox != null)
            {
                ShowBip39PassphraseCheckBox.IsEnabled = enabled;
                ShowBip39PassphraseCheckBox.IsChecked = false;
            }

            if (!enabled)
            {
                if (PassphrasePasswordBox != null) PassphrasePasswordBox.Password = string.Empty;
                if (PassphraseTextBox != null) PassphraseTextBox.Text = string.Empty;
                SwapPassphraseVisibility(showPlainText: false);
            }

            UpdateAuthoriseEnabled();

            RefreshSeedAuthoriseState();
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

            RefreshSeedAuthoriseState();
                    NotifyRecoveryInputsChangedIfSelectionBecameInvalid();
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

            RefreshSeedAuthoriseState();
                    NotifyRecoveryInputsChangedIfSelectionBecameInvalid();
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

            RefreshSeedAuthoriseState();
                    NotifyRecoveryInputsChangedIfSelectionBecameInvalid();
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

            RefreshSeedAuthoriseState();
                    NotifyRecoveryInputsChangedIfSelectionBecameInvalid();
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

        private void WordBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Tab)
                return;

            if (sender is not FrameworkElement element)
                return;

            if (element.DataContext is not WordEntry entry || entry.IsPlaceholder)
                return;

            int direction = Keyboard.Modifiers == ModifierKeys.Shift ? -1 : 1;
            int nextIndex = FindNextWordEntryIndex(entry.Index, direction);

            if (nextIndex < 0)
            {
                e.Handled = true;
                return;
            }

            e.Handled = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                FocusWordEntry(nextIndex);
            }));
        }

        private int FindNextWordEntryIndex(int currentIndex, int direction)
        {
            int i = currentIndex + direction;

            while (i >= 0 && i < _wordEntries.Count)
            {
                if (!_wordEntries[i].IsPlaceholder)
                    return i;

                i += direction;
            }

            return -1;
        }

        private void FocusWordEntry(int entryIndex)
        {
            if (WordsItemsControl == null || entryIndex < 0 || entryIndex >= _wordEntries.Count)
                return;

            var container = WordsItemsControl.ItemContainerGenerator.ContainerFromIndex(entryIndex) as FrameworkElement;
            if (container == null)
                return;

            var cell = FindChild<Border>(container, "WordCellRoot");
            if (cell == null)
                return;

            bool showPlain = ShowPhraseCheckBox?.IsChecked == true;

            if (showPlain)
            {
                var tb = FindChild<TextBox>(cell, "WordTextBox");
                if (tb != null)
                {
                    tb.Focus();
                    tb.CaretIndex = tb.Text?.Length ?? 0;
                }

                return;
            }

            var pb = FindChild<PasswordBox>(cell, "WordPasswordBox");
            if (pb != null)
            {
                pb.Focus();
            }
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

        private bool IsSeedPhraseComplete(out string phrase)
        {
            phrase = string.Empty;

            if (_wordEntries == null || _wordEntries.Count == 0)
                return false;

            var words = new List<string>();

            foreach (var entry in _wordEntries)
            {
                if (entry.IsPlaceholder)
                    continue;

                var text = entry.Text?.Trim();

                if (string.IsNullOrWhiteSpace(text))
                    return false;

                words.Add(text);
            }

            int expected = GetExpectedSeedWordCount();

            if (words.Count != expected)
                return false;

            phrase = string.Join(" ", words);
            return true;
        }

        private int GetExpectedSeedWordCount()
        {
            if (PhraseLengthComboBox?.SelectedItem is ComboBoxItem item)
            {
                if (int.TryParse(item.Content?.ToString()?.Split(' ')[0], out int count))
                    return count;
            }

            return 12;
        }
        private string GetCurrentBip39Passphrase()
        {
            if (UsePassphraseCheckBox?.IsChecked != true)
                return string.Empty;

            if (ShowBip39PassphraseCheckBox?.IsChecked == true)
                return PassphraseTextBox?.Text?.Trim() ?? string.Empty;

            return PassphrasePasswordBox?.Password ?? string.Empty;
        }


        private string GetSelectedDerivationPathTemplate()
        {
            var text = (DerivationPathComboBox?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;

            if (text.Contains("m/44'/550'/0'/0/i", StringComparison.OrdinalIgnoreCase))
                return "m/44'/550'/0'/0/i";

            if (text.Contains("m/44'/60'/0'/0/i", StringComparison.OrdinalIgnoreCase))
                return "m/44'/60'/0'/0/i";
            return "m/44'/550'/0'/0/i";
        }

        private void UpdateSeedSelectedActionButtons()
        {
            bool seedReady = AuthTabControl?.SelectedIndex == 1 &&
                             IsSeedPhraseComplete(out _);

            bool bip39Ok = UsePassphraseCheckBox?.IsChecked != true ||
                           !string.IsNullOrWhiteSpace(GetCurrentBip39Passphrase());

            bool hasSelection = _selectedDerivedSeedAccount != null;
            bool inputsStillMatch = hasSelection &&
                                    SelectedDerivedAddressStillMatchesCurrentInputs(out _);

            bool canUseSelectedActions = seedReady && bip39Ok && hasSelection && inputsStillMatch;

            if (CreateKeystoreSelectedButton != null)
                CreateKeystoreSelectedButton.IsEnabled = canUseSelectedActions;

            if (ExportPrivateKeySelectedButton != null)
                ExportPrivateKeySelectedButton.IsEnabled = canUseSelectedActions;
        }
        private void NotifyRecoveryInputsChangedIfSelectionBecameInvalid()
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            if (_selectedDerivedSeedAccount == null)
            {
                _seedSelectionInvalidatedNoticeShown = false;
                return;
            }

            if (SelectedDerivedAddressStillMatchesCurrentInputs(out _))
            {
                _seedSelectionInvalidatedNoticeShown = false;
                return;
            }

            if (!_seedSelectionInvalidatedNoticeShown)
            {
                _seedSelectionInvalidatedNoticeShown = true;

                MessageBox.Show(
                    "You changed the seed phrase or BIP39 passphrase after scanning addresses.\n\nPlease click Scan now again and reselect the correct address before continuing.",
                    "Recovery inputs changed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            UpdateSeedSelectedActionButtons();
            RefreshSeedAuthoriseState();
        }

        private int GetSelectedScanCount()
        {
            if (ScanCountComboBox?.SelectedItem is ComboBoxItem item &&
                int.TryParse(item.Content?.ToString(), out int count) &&
                count > 0)
            {
                return count;
            }

            return 12;
        }

        private void ClearDerivedSeedAccounts()
        {
            _derivedSeedAccounts.Clear();
            _selectedDerivedSeedAccount = null;

            if (DerivedAddressesDataGrid != null)
                DerivedAddressesDataGrid.SelectedItem = null;

            if (SelectedAddressSummaryTextBlock != null)
                SelectedAddressSummaryTextBlock.Text = "Selected address: None";

            if (CopySelectedAddressButton != null)
                CopySelectedAddressButton.IsEnabled = false;

            if (CreateKeystoreSelectedButton != null)
                CreateKeystoreSelectedButton.IsEnabled = false;

            if (ExportPrivateKeySelectedButton != null)
                ExportPrivateKeySelectedButton.IsEnabled = false;
        }

        private bool IsRecoverySectionDirty()
        {
            if (_wordEntries.Any(w => !w.IsPlaceholder && !string.IsNullOrWhiteSpace(w.Text)))
                return true;

            if (!string.IsNullOrWhiteSpace(PassphrasePasswordBox?.Password))
                return true;

            if (!string.IsNullOrWhiteSpace(PassphraseTextBox?.Text))
                return true;

            if (_derivedSeedAccounts.Count > 0)
                return true;

            if (_selectedDerivedSeedAccount != null)
                return true;

            return false;
        }

        private void ClearRecoverySection()
        {
            _suppressUiEvents = true;
            try
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

                ClearDerivedSeedAccounts();
            }
            finally
            {
                _suppressUiEvents = false;
            }

            UpdateAuthoriseEnabled();
            RefreshSeedAuthoriseState();
            NotifyRecoveryInputsChangedIfSelectionBecameInvalid();
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

        private bool IsPrivateKeySectionDirty()
        {
            return
                !string.IsNullOrWhiteSpace(PrivateKeyPasswordBox?.Password) ||
                !string.IsNullOrWhiteSpace(PrivateKeyTextBox?.Text) ||
                !string.IsNullOrWhiteSpace(_privateKeyValidatedHex0x) ||
                !string.IsNullOrWhiteSpace(_privateKeyValidatedAddress0x) ||
                !string.IsNullOrWhiteSpace(_privateKeyValidatedAddressXdc);
        }

        private void ClearPrivateKeySection()
        {
            _suppressUiEvents = true;
            try
            {
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
            }
            finally
            {
                _suppressUiEvents = false;
            }

            UpdateAuthoriseEnabled();
            RefreshSeedAuthoriseState();
            NotifyRecoveryInputsChangedIfSelectionBecameInvalid();
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

            ClearPrivateKeySection();
            return true;
        }

        private void RefreshSeedScanUi()
        {
            bool readyForSeedActions = AuthTabControl?.SelectedIndex == 1 &&
                                       IsSeedPhraseComplete(out _);

            if (DerivationPathComboBox != null)
            {
                DerivationPathComboBox.IsEnabled = readyForSeedActions;
                if (DerivationPathComboBox.SelectedIndex < 0)
                    DerivationPathComboBox.SelectedIndex = 0;
            }

            if (ScanCountComboBox != null)
                ScanCountComboBox.IsEnabled = readyForSeedActions;

            if (ScanNowButton != null)
                ScanNowButton.IsEnabled = readyForSeedActions;

            if (!readyForSeedActions)
                ClearDerivedSeedAccounts();

            UpdateSeedSelectedActionButtons();
        }

        private async void ScanNowButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsSeedPhraseComplete(out var phrase))
            {
                MessageBox.Show(
                    "Please complete the seed phrase before scanning.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                var bip39Passphrase = GetCurrentBip39Passphrase();
                var derivationPathTemplate = GetSelectedDerivationPathTemplate();
                var scanCount = GetSelectedScanCount();
                var rpcUrl = AppSession.Instance.CurrentRpcUrl;
                var web3 = new Web3(rpcUrl);

                _derivedSeedAccounts.Clear();

                var derived = WalletDerivationService.DeriveManyFromMnemonic(
                    phrase,
                    bip39Passphrase,
                    derivationPathTemplate,
                    scanCount);

                foreach (var item in derived)
                {
                    string balanceXdcText;

                    try
                    {
                        var balanceWei = await web3.Eth.GetBalance.SendRequestAsync(item.Address0x);
                        balanceXdcText = UnitConversion.Convert.FromWei(balanceWei)
                            .ToString("0.#########", CultureInfo.InvariantCulture);
                    }
                    catch
                    {
                        balanceXdcText = "—";
                    }

                    _derivedSeedAccounts.Add(new DerivedSeedAccountItem(
                        item.Index,
                        item.PrivateKeyHex0x,
                        item.Address0x,
                        item.AddressXdc,
                        item.DerivationPathUsed,
                        balanceXdcText));
                }

                if (_derivedSeedAccounts.Count > 0)
                {
                    if (DerivedAddressesDataGrid != null)
                        DerivedAddressesDataGrid.SelectedIndex = 0;
                }
                else
                {
                    ClearDerivedSeedAccounts();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to scan derived addresses. " + ex.Message,
                    "Scan Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        private void DerivedAddressesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedDerivedSeedAccount = DerivedAddressesDataGrid?.SelectedItem as DerivedSeedAccountItem;

            if (_selectedDerivedSeedAccount == null)
            {
                if (SelectedAddressSummaryTextBlock != null)
                    SelectedAddressSummaryTextBlock.Text = "Selected address: None";

                if (CopySelectedAddressButton != null)
                    CopySelectedAddressButton.IsEnabled = false;

                if (CreateKeystoreSelectedButton != null)
                    CreateKeystoreSelectedButton.IsEnabled = false;

                if (ExportPrivateKeySelectedButton != null)
                ExportPrivateKeySelectedButton.IsEnabled = false;

            UpdateSeedSelectedActionButtons();
            RefreshSeedAuthoriseState();
            return;
            }

            if (SelectedAddressSummaryTextBlock != null)
            {
                SelectedAddressSummaryTextBlock.Text =
                    $"Selected address: {_selectedDerivedSeedAccount.AddressXdc} (index {_selectedDerivedSeedAccount.Index})";
            }

            if (CopySelectedAddressButton != null)
                CopySelectedAddressButton.IsEnabled = true;

            UpdateSeedSelectedActionButtons();
            UpdateAuthoriseEnabled();
            RefreshSeedAuthoriseState();
            NotifyRecoveryInputsChangedIfSelectionBecameInvalid();
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

            RefreshSeedAuthoriseState();
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

            RefreshSeedAuthoriseState();
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
            }            var main = Window.GetWindow(this) as MainWindow;
            if (main == null)
                return;

            main.ShowGenerateKeystoreOverlay(
                _privateKeyValidatedAddressXdc,
                _privateKeyValidatedAddress0x,
                _privateKeyValidatedHex0x,
                "Direct private key import",
                false);}

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

        private void AuthTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            UpdateAuthoriseEnabled();

            RefreshSeedAuthoriseState();
        }

        private void UpdateAuthoriseEnabled()
        {
            if (!_isWindowReady || AuthoriseButton == null || AuthTabControl == null)
                return;

            try
            {
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
                    AuthoriseButton.IsEnabled = false;
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
                AuthoriseButton.IsEnabled = false;

                if (CopyPrivateKeyAddressButton != null)
                    CopyPrivateKeyAddressButton.IsEnabled = false;

                if (CreateKeystoreFromPrivateKeyButton != null)
                    CreateKeystoreFromPrivateKeyButton.IsEnabled = false;
            }
        }

        private void Authorise_Click(object sender, RoutedEventArgs e)
        {
            if (AuthTabControl?.SelectedIndex == 1)
            {
                if (!IsSeedPhraseComplete(out var phrase))
                {
                    MessageBox.Show(
                        "Please complete the seed phrase before authorising.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    var bip39Passphrase = GetCurrentBip39Passphrase();
                    var derivationPathTemplate = GetSelectedDerivationPathTemplate();

                    if (_selectedDerivedSeedAccount == null)
{
    MessageBox.Show(
        "Please select an address from the scanned list.",
        "Selection Required",
        MessageBoxButton.OK,
        MessageBoxImage.Warning);
    return;
}
                    if (!SelectedDerivedAddressStillMatchesCurrentInputs(out var mismatchMessage))
                    {
                        MessageBox.Show(
                            mismatchMessage,
                            "Recovery inputs changed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }


var derived = _selectedDerivedSeedAccount;

                    AuthorisedSeedPhrase = phrase;
                    AppSession.Instance.SetWallet(derived.PrivateKeyHex0x, derived.Address0x);

                    Result = true;
                    CloseOverlay();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Unable to authorise from seed phrase. " + ex.Message,
                        "Authorisation Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                return;
            }

            if (AuthTabControl?.SelectedIndex == 2)
            {
                AuthoriseFromPrivateKey();
                return;
            }

            AuthoriseFromKeystore();
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
                    using var _ = System.Text.Json.JsonDocument.Parse(json);
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

                Result = true;
                CloseOverlay();
            }
            catch
            {
                MessageBox.Show(
                    "Unable to decrypt keystore. Check your password and try again.",
                    "Authorisation Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
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

                Result = true;
                CloseOverlay();
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

        private static string Ensure0x(string pk)
        {
            if (string.IsNullOrWhiteSpace(pk))
                return pk;

            return pk.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? pk : "0x" + pk;
        }

        private async void CopySelectedAddressButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedDerivedSeedAccount == null)
                return;

            try
            {
                Clipboard.SetText(_selectedDerivedSeedAccount.AddressXdc);

                if (sender is Button btn)
                {
                    var original = btn.Content;
                    btn.Content = "Copied";
                    btn.IsEnabled = false;

                    await System.Threading.Tasks.Task.Delay(1000);

                    btn.Content = original;
                    btn.IsEnabled = _selectedDerivedSeedAccount != null;
                }
            }
            catch
            {
                MessageBox.Show(
                    "Could not copy selected address.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void CreateKeystoreSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            if (_selectedDerivedSeedAccount == null)
            {
                MessageBox.Show(
                    "Please select an address first.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (UsePassphraseCheckBox?.IsChecked == true &&
                string.IsNullOrWhiteSpace(GetCurrentBip39Passphrase()))
            {
                MessageBox.Show(
                    "BIP39 passphrase is enabled but no passphrase was entered.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!IsSeedPhraseComplete(out var mnemonic))
            {
                MessageBox.Show(
                    "Please enter a complete 12 or 24 word seed phrase.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            if (!SelectedDerivedAddressStillMatchesCurrentInputs(out var mismatchMessage))
            {
                MessageBox.Show(
                    mismatchMessage,
                    "Recovery inputs changed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }


            

            try
            {
                var main = Window.GetWindow(this) as MainWindow;
                if (main == null)
                    return;

                var bip39Passphrase = GetCurrentBip39Passphrase();

                main.ShowGenerateKeystoreOverlay(
                    _selectedDerivedSeedAccount.AddressXdc,
                    _selectedDerivedSeedAccount.Address0x,
                    _selectedDerivedSeedAccount.PrivateKeyHex0x,
                    _selectedDerivedSeedAccount.DerivationPathUsed,
                    !string.IsNullOrWhiteSpace(bip39Passphrase),
                    mnemonic,
                    !string.IsNullOrWhiteSpace(bip39Passphrase));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to open Generate Keystore for the selected address.\n\n" + ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool SelectedDerivedAddressStillMatchesCurrentInputs(out string mismatchMessage)
        {
            mismatchMessage = string.Empty;

            if (_selectedDerivedSeedAccount == null)
                return true;

            if (!IsSeedPhraseComplete(out var mnemonic))
            {
                mismatchMessage =
                    "The seed phrase is no longer complete.\n\n" +
                    "Please scan addresses again and reselect the correct address before continuing.";
                return false;
            }

            try
            {
                var currentPassphrase = GetCurrentBip39Passphrase();
                string template = GetSelectedDerivationPathTemplate();

                var derived = WalletDerivationService.DeriveFromMnemonic(
                    mnemonicPhrase: mnemonic,
                    bip39Passphrase: currentPassphrase,
                    derivationPathTemplate: template,
                    index: _selectedDerivedSeedAccount.Index);

                if (!string.Equals(
                    derived.AddressXdc?.Trim(),
                    _selectedDerivedSeedAccount.AddressXdc?.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                {
                    mismatchMessage =
                        "You changed the seed phrase, BIP39 passphrase, or derivation path after scanning addresses.\n\n" +
                        "Please scan addresses again and reselect the correct address before continuing.";
                    return false;
                }

                return true;
            }
            catch
            {
                mismatchMessage =
                    "The current recovery inputs could not be validated against the selected address.\n\n" +
                    "Please scan addresses again and reselect the correct address before continuing.";
                return false;
            }
        }

        private void ExportPrivateKeySelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isWindowReady || _suppressUiEvents)
                return;

            if (_selectedDerivedSeedAccount == null)
            {
                MessageBox.Show(
                    "Please select an address first.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!SelectedDerivedAddressStillMatchesCurrentInputs(out var mismatchMessage))
            {
                MessageBox.Show(
                    mismatchMessage,
                    "Recovery inputs changed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var main = Window.GetWindow(this) as MainWindow;
            if (main == null)
                return;

            main.ShowExportPrivateKeyOverlay(
                _selectedDerivedSeedAccount.AddressXdc,
                Ensure0x(_selectedDerivedSeedAccount.PrivateKeyHex0x));
        }

        private sealed class DerivedSeedAccountItem
        {
            public DerivedSeedAccountItem(int index, string privateKeyHex0x, string address0x, string addressXdc, string derivationPathUsed, string balanceXdc)
            {
                Index = index;
                PrivateKeyHex0x = privateKeyHex0x;
                Address0x = address0x;
                AddressXdc = addressXdc;
                DerivationPathUsed = derivationPathUsed;
                BalanceXdc = balanceXdc;
            }

            public int Index { get; }
            public string PrivateKeyHex0x { get; }
            public string Address0x { get; }
            public string AddressXdc { get; }
            public string DerivationPathUsed { get; }
            public string BalanceXdc { get; }
            public string DisplayText => $"[{Index}] {AddressXdc}    {DerivationPathUsed}";
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

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            CloseOverlay();
        }

        private void AuthorisationOverlayControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Result = false;
                CloseOverlay();
            }
        }

        private void Overlay_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Tab)
                return;

            var request = new TraversalRequest(
                Keyboard.Modifiers == ModifierKeys.Shift
                    ? FocusNavigationDirection.Previous
                    : FocusNavigationDirection.Next);

            if (Keyboard.FocusedElement is UIElement element)
            {
                if (!element.MoveFocus(request))
                {
                    e.Handled = true;
                }
            }
        }

        private void CloseOverlay()
        {
            if (Application.Current.MainWindow is MainWindow main)
            {
                main.CloseModalOverlay();
            }
        }
    }
}







































