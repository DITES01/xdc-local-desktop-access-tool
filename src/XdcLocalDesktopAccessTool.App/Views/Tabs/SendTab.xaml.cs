using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using XdcLocalDesktopAccessTool.App.Services;
using XdcLocalDesktopAccessTool.App.Views.Windows;

namespace XdcLocalDesktopAccessTool.App.Views.Tabs
{
    public partial class SendTab : UserControl
    {
        private bool _uiReady;
        private decimal? _suggestedGasGwei;

        private const long DefaultGasLimit = 21000;
        private const string DonationAddressXdc = "xdcd8DFc137957CaCe772021a84019E658DEFECCF43";

        public SendTab()
        {
            InitializeComponent();

            _uiReady = false;

            Loaded += async (_, __) =>
            {
                _uiReady = true;

                RefreshAuthUi();
                UpdateToAddressUi();
                ApplyGasModeUi();
                await RefreshSuggestedGasAsync();
            };
        }

        // --------------------------------------------------
        // AUTHORISATION UI
        // --------------------------------------------------

        private void RefreshAuthUi()
        {
            bool authorised = AppSession.Instance.IsAuthorised;

            if (authorised)
            {
                var addr0x = TryGetFromAddress0x();
                var addrXdc = string.IsNullOrWhiteSpace(addr0x) ? null : ToXdcAddress(addr0x);

                AuthorisationStatusText.Text = "Authorised (local signing)";
                AuthorisationStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2E7D32"));

                FromAddressTextBox.Text = addrXdc ?? string.Empty;

                AuthoriseButton.Visibility = Visibility.Collapsed;
                DeauthoriseButton.Visibility = Visibility.Visible;
            }
            else
            {
                AuthorisationStatusText.Text = "Not authorised";
                AuthorisationStatusText.Foreground = System.Windows.Media.Brushes.DarkRed;

                FromAddressTextBox.Text = string.Empty;

                AuthoriseButton.Visibility = Visibility.Visible;
                DeauthoriseButton.Visibility = Visibility.Collapsed;
            }

            CopyAddressButton.IsEnabled = authorised &&
                                          !string.IsNullOrWhiteSpace(TryGetFromAddress0x());

            CopyAddressButton.ToolTip = CopyAddressButton.IsEnabled
                ? "Copy your authorised address."
                : "Authorise to enable copying.";

            RefreshActionButtons();
        }

        private void RefreshActionButtons()
        {
            var authorised = AppSession.Instance.IsAuthorised;
            var hasLastTx = !string.IsNullOrWhiteSpace(AppSession.Instance.LastTxHash);

            var addr = (ToAddressTextBox.Text ?? string.Empty).Trim();
            var validAddress = IsFormatValidXdcOr0x(addr);

            var validAmount = decimal.TryParse(
                AmountTextBox.Text,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var amt) && amt > 0m;

            bool validGas = true;

            if (GasModeCombo.SelectedIndex == 1)
            {
                validGas = decimal.TryParse(
                    CustomGasTextBox.Text,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var customGas) && customGas > 0m;
            }

            PreviewButton.IsEnabled = authorised && validAddress && validAmount && validGas;
            SendButton.IsEnabled = authorised && validAddress && validAmount && validGas;
            ViewLastTxButton.IsEnabled = authorised && hasLastTx;

            ToAddressTextBox.IsEnabled = authorised;
            AmountTextBox.IsEnabled = authorised;
            GasModeCombo.IsEnabled = authorised;
            ClearButton.IsEnabled = authorised;

            CustomGasTextBox.IsEnabled = authorised && (GasModeCombo.SelectedIndex == 1);

            ViewToAddressButton.IsEnabled = authorised && validAddress;

            UpdateToAddressUi();
        }

        private void Authorise_Click(object sender, RoutedEventArgs e)
        {
            var win = new AuthorisationWindow
            {
                Owner = Window.GetWindow(this)
            };

            if (win.ShowDialog() == true)
            {
                RefreshAuthUi();
                AppendLine("Authorised.");
            }
        }

        private void Deauthorise_Click(object sender, RoutedEventArgs e)
        {
            if (!AppSession.Instance.IsAuthorised)
                return;

            var result = MessageBox.Show(
                "This will clear the currently authorised wallet from memory.\n\n" +
                "The address, private key access, and any transaction data in this session will be removed.\n\n" +
                "You will need to authorise again to sign transactions.\n\n" +
                "Do you want to continue?",
                "Confirm Deauthorisation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            AppSession.Instance.ClearAuthorisation();
            AppSession.Instance.LastTxHash = string.Empty;

            ClearSendEntryFields();
            OutputText.Text = "Deauthorised (memory cleared).";

            RefreshAuthUi();
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            ClearSendEntryFields();
            OutputText.Text = string.Empty;
            RefreshActionButtons();
        }

        private void ClearSendEntryFields()
        {
            ToAddressTextBox.Text = string.Empty;
            AmountTextBox.Text = string.Empty;
            CustomGasTextBox.Text = string.Empty;
            GasModeCombo.SelectedIndex = 0;
        }

        public bool HasDataToLoseForTabChange()
        {
            return AppSession.Instance.IsAuthorised
                   || !string.IsNullOrWhiteSpace(ToAddressTextBox.Text)
                   || !string.IsNullOrWhiteSpace(AmountTextBox.Text)
                   || !string.IsNullOrWhiteSpace(CustomGasTextBox.Text)
                   || !string.IsNullOrWhiteSpace(OutputText.Text)
                   || GasModeCombo.SelectedIndex != 0;
        }

        public void DeauthoriseAndClearForTabChange()
        {
            AppSession.Instance.ClearAuthorisation();
            AppSession.Instance.LastTxHash = string.Empty;

            ToAddressTextBox.Text = string.Empty;
            AmountTextBox.Text = string.Empty;
            CustomGasTextBox.Text = string.Empty;
            OutputText.Text = string.Empty;
            GasModeCombo.SelectedIndex = 0;

            RefreshAuthUi();
        }

        // --------------------------------------------------
        // COPY ADDRESS
        // --------------------------------------------------

        private async void CopyAddress_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var from0x = TryGetFromAddress0x();
                if (string.IsNullOrWhiteSpace(from0x))
                    return;

                var fromXdc = ToXdcAddress(from0x);
                Clipboard.SetText(fromXdc);

                if (sender is Button btn)
                {
                    var original = btn.Content;
                    btn.Content = "Copied";
                    btn.IsEnabled = false;

                    await Task.Delay(1000);

                    btn.Content = original;
                    btn.IsEnabled = AppSession.Instance.IsAuthorised &&
                                    !string.IsNullOrWhiteSpace(TryGetFromAddress0x());
                }
            }
            catch
            {
                MessageBox.Show("Could not copy address.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --------------------------------------------------
        // TO ADDRESS LIVE VALIDATION
        // --------------------------------------------------

        private void ToAddressTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_uiReady) return;
            UpdateToAddressUi();
            RefreshActionButtons();
        }

        private void AmountTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_uiReady) return;
            RefreshActionButtons();
        }

        private void CustomGasTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_uiReady) return;
            RefreshActionButtons();
        }

        private void UpdateToAddressUi()
        {
            var authorised = AppSession.Instance.IsAuthorised;
            var addr = (ToAddressTextBox.Text ?? string.Empty).Trim();

            var valid = IsFormatValidXdcOr0x(addr);

            ToAddressValidTick.Visibility = valid ? Visibility.Visible : Visibility.Hidden;

            ViewToAddressButton.IsEnabled = authorised && valid;

            if (!valid)
                ViewToAddressButton.ToolTip = "Enter a valid address to enable.";
            else if (!authorised)
                ViewToAddressButton.ToolTip = "Authorise to enable viewing on XDCScan.";
            else
                ViewToAddressButton.ToolTip = "View address on XDCScan.";
        }

        private void ViewToAddress_Click(object sender, RoutedEventArgs e)
        {
            var addr = (ToAddressTextBox.Text ?? string.Empty).Trim();
            if (!IsFormatValidXdcOr0x(addr))
            {
                MessageBox.Show("Destination address format is invalid.",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var xdc = addr.StartsWith("xdc", StringComparison.OrdinalIgnoreCase) ? addr : ToXdcAddress(addr);
            SafeOpen($"https://xdcscan.com/address/{xdc}");
        }

        // --------------------------------------------------
        // GAS MODE UI
        // --------------------------------------------------

        private void GasMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_uiReady) return;
            ApplyGasModeUi();
            RefreshActionButtons();
        }

        private void ApplyGasModeUi()
        {
            var manual = GasModeCombo.SelectedIndex == 1;
            CustomGasTextBox.Visibility = manual ? Visibility.Visible : Visibility.Collapsed;
            GasPriceLabel.Visibility = manual ? Visibility.Visible : Visibility.Collapsed;
            CustomGasTextBox.Opacity = manual ? 1.0 : 0.55;
            CustomGasTextBox.IsEnabled = AppSession.Instance.IsAuthorised && manual;
        }

        private async Task RefreshSuggestedGasAsync()
        {
            try
            {
                var rpc = AppSession.Instance.CurrentRpcUrl;
                _suggestedGasGwei = await RpcGas.GetGasPriceGweiAsync(rpc);

                SuggestedGasText.Text = _suggestedGasGwei.HasValue
                    ? $"Suggested gas: {_suggestedGasGwei.Value:0.##} gwei"
                    : "Suggested gas: —";
            }
            catch
            {
                SuggestedGasText.Text = "Suggested gas: —";
            }
        }

        // --------------------------------------------------
        // PREVIEW / SEND / LAST TX
        // --------------------------------------------------

        private async void Preview_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureAuthorisedWithKey()) return;
            if (!ValidateInputs(out var toXdc, out var amountXdc, out var gasGwei)) return;

            var from0x = TryGetFromAddress0x() ?? "";
            var fromXdc = string.IsNullOrWhiteSpace(from0x) ? "—" : ToXdcAddress(from0x);
            var estimatedNetworkFeeXdc = (gasGwei * DefaultGasLimit) / 1_000_000_000m;
            var maximumTotalCostXdc = amountXdc + estimatedNetworkFeeXdc;

            decimal? currentBalanceXdc = null;
            decimal? maximumSpendableXdc = null;

            try
            {
                currentBalanceXdc = await GetBalanceXdcAsync(AppSession.Instance.CurrentRpcUrl, from0x);
                maximumSpendableXdc = currentBalanceXdc.Value - estimatedNetworkFeeXdc;
                if (maximumSpendableXdc < 0m)
                    maximumSpendableXdc = 0m;
            }
            catch
            {
                // Leave balance values null if RPC lookup fails during preview.
            }

            var sb = new StringBuilder();
            sb.AppendLine("Preview:");
            sb.AppendLine($"RPC: {AppSession.Instance.CurrentRpcUrl}");
            sb.AppendLine($"From: {fromXdc}");
            sb.AppendLine($"To: {toXdc}");
            sb.AppendLine($"Amount: {amountXdc:0.################} XDC");
            sb.AppendLine($"Gas price: {gasGwei:0.##} gwei");
            sb.AppendLine($"Gas limit: {DefaultGasLimit}");
            sb.AppendLine($"Estimated network fee: {estimatedNetworkFeeXdc:0.################} XDC");
            sb.AppendLine($"Maximum total cost: {maximumTotalCostXdc:0.################} XDC");

            if (currentBalanceXdc.HasValue)
            {
                sb.AppendLine($"Current balance: {currentBalanceXdc.Value:0.################} XDC");
                sb.AppendLine($"Maximum spendable now: {maximumSpendableXdc!.Value:0.################} XDC");
            }

            sb.AppendLine($"ChainId: {AppSession.XdcChainId}");

            OutputText.Text = sb.ToString().TrimEnd();
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureAuthorisedWithKey()) return;
            if (!ValidateInputs(out var toXdc, out var amountXdc, out var gasGwei)) return;

            var from0x = TryGetFromAddress0x() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(from0x))
            {
                MessageBox.Show(
                    "Authorisation is missing wallet address information.\n\nPlease deauthorise and authorise again.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            var fromXdc = ToXdcAddress(from0x);
            var networkFeeXdc = (gasGwei * DefaultGasLimit) / 1_000_000_000m;
            var maximumTotalCostXdc = amountXdc + networkFeeXdc;
            var isDonationSend = string.Equals(
                ToCanonicalAddress(toXdc),
                ToCanonicalAddress(DonationAddressXdc),
                StringComparison.OrdinalIgnoreCase);

            decimal currentBalanceXdc;
            try
            {
                currentBalanceXdc = await GetBalanceXdcAsync(AppSession.Instance.CurrentRpcUrl, from0x);
            }
            catch
            {
                MessageBox.Show(
                    "Unable to verify the current wallet balance.\n\nPlease check your RPC connection and try again.",
                    "Balance Check Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (currentBalanceXdc < maximumTotalCostXdc)
            {
                MessageBox.Show(
                    "The wallet does not have enough XDC to cover both the transaction amount and the estimated network fee.\n\n" +
                    "Please reduce the amount or lower the gas price.",
                    "Insufficient Funds",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                "Please confirm this transaction.\n\n" +
                $"From: {fromXdc}\n" +
                $"To: {toXdc}\n" +
                $"Amount: {amountXdc:0.################} XDC\n" +
                $"Gas price: {gasGwei:0.##} gwei\n" +
                $"Gas limit: {DefaultGasLimit}\n" +
                $"Estimated network fee: {networkFeeXdc:0.################} XDC\n" +
                $"Maximum total cost: {maximumTotalCostXdc:0.################} XDC\n\n" +
                "Do you want to continue?",
                "Confirm Transaction",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                var pk = AppSession.Instance.PrivateKeyHex!;
                var rpc = AppSession.Instance.CurrentRpcUrl;

                var account = new Account(pk, AppSession.XdcChainId);
                var web3 = new Web3(account, rpc);

                var to0x = toXdc.StartsWith("xdc", StringComparison.OrdinalIgnoreCase)
                    ? "0x" + toXdc.Substring(3)
                    : toXdc;

                BigInteger valueWei = UnitConversion.Convert.ToWei(amountXdc);
                BigInteger gasPriceWei = UnitConversion.Convert.ToWei(gasGwei, UnitConversion.EthUnit.Gwei);

                var nonce = await web3.Eth.Transactions.GetTransactionCount
                    .SendRequestAsync(account.Address, BlockParameter.CreatePending());

                var tx = new TransactionInput
                {
                    From = account.Address,
                    To = to0x,
                    Value = new HexBigInteger(valueWei),
                    GasPrice = new HexBigInteger(gasPriceWei),
                    Gas = new HexBigInteger(DefaultGasLimit),
                    Nonce = new HexBigInteger(nonce)
                };

                AppendLine(string.Empty);
                AppendLine("Broadcasting transaction...");
                var txHash = await web3.Eth.TransactionManager.SendTransactionAsync(tx);

                AppSession.Instance.LastTxHash = txHash;
                AppendLine($"Broadcasted: {txHash}");
                AppendLine("Waiting for confirmation...");

                TransactionReceipt? receipt = null;

                while (receipt == null)
                {
                    await Task.Delay(3000);
                    receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
                }

                if (receipt.BlockNumber != null)
                    AppendLine($"Transaction confirmed in block {receipt.BlockNumber.Value}.");

                if (receipt.Status != null && receipt.Status.Value == 1)
                    AppendLine("Status: SUCCESS");
                else
                    AppendLine("Status: FAILED");

                ToAddressTextBox.Text = string.Empty;
                AmountTextBox.Text = string.Empty;

                if (GasModeCombo.SelectedIndex == 1)
                    CustomGasTextBox.Text = string.Empty;

                RefreshActionButtons();

                if (isDonationSend)
                {
                    var thankYou = new ThankYouWindow
                    {
                        Owner = Window.GetWindow(this)
                    };
                    thankYou.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                var lower = ex.Message?.ToLowerInvariant() ?? string.Empty;

                if (lower.Contains("insufficient funds"))
                {
                    MessageBox.Show(
                        "The wallet does not have enough XDC to cover the transaction amount and the network fee.",
                        "Insufficient Funds",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show(
                        "Failed to sign or broadcast the transaction.",
                        "Transaction Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                AppendLine(string.Empty);
                AppendLine("Send failed.");
            }
        }

        private void ViewLastTx_Click(object sender, RoutedEventArgs e)
        {
            var tx = AppSession.Instance.LastTxHash;
            if (string.IsNullOrWhiteSpace(tx)) return;

            SafeOpen($"https://xdcscan.com/tx/{tx}");
        }

        // --------------------------------------------------
        // VALIDATION + HELPERS
        // --------------------------------------------------

        private bool EnsureAuthorisedWithKey()
        {
            if (!AppSession.Instance.IsAuthorised)
            {
                MessageBox.Show("Please authorise before sending.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (string.IsNullOrWhiteSpace(AppSession.Instance.PrivateKeyHex) ||
                string.IsNullOrWhiteSpace(TryGetFromAddress0x()))
            {
                MessageBox.Show(
                    "Authorisation is missing wallet data.\n\nPlease deauthorise and authorise again using a keystore file.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        private bool ValidateInputs(out string toXdc, out decimal amountXdc, out decimal gasGwei)
        {
            toXdc = string.Empty;
            amountXdc = 0m;
            gasGwei = 0m;

            var to = (ToAddressTextBox.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(to))
            {
                MessageBox.Show("Destination address is required.",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!IsFormatValidXdcOr0x(to))
            {
                MessageBox.Show("Destination address format is invalid.",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            toXdc = to.StartsWith("xdc", StringComparison.OrdinalIgnoreCase) ? to : ToXdcAddress(to);

            var from0x = TryGetFromAddress0x() ?? string.Empty;

            if (string.Equals(
                ToCanonicalAddress(from0x),
                ToCanonicalAddress(toXdc),
                StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "Destination address matches the authorised sending address.\n\nPlease enter a different recipient address.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                ToAddressTextBox.Focus();
                ToAddressTextBox.SelectAll();
                return false;
            }

            var amtRaw = (AmountTextBox.Text ?? string.Empty).Trim();
            if (!TryParseAndValidateAmount(amtRaw, out amountXdc, out var amountTitle, out var amountMessage))
            {
                MessageBox.Show(amountMessage,
                    amountTitle, MessageBoxButton.OK, MessageBoxImage.Warning);

                AmountTextBox.Focus();
                AmountTextBox.SelectAll();
                return false;
            }

            if (GasModeCombo.SelectedIndex == 1)
            {
                var gasRaw = (CustomGasTextBox.Text ?? string.Empty).Trim();
                if (!decimal.TryParse(gasRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out gasGwei) || gasGwei <= 0m)
                {
                    MessageBox.Show("Custom gas price must be greater than 0.",
                        "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);

                    CustomGasTextBox.Focus();
                    CustomGasTextBox.SelectAll();
                    return false;
                }
            }
            else
            {
                gasGwei = _suggestedGasGwei ?? 0m;
                if (gasGwei <= 0m)
                {
                    MessageBox.Show("Could not get suggested gas price.",
                        "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        private static bool TryParseAndValidateAmount(
            string raw,
            out decimal amount,
            out string title,
            out string message)
        {
            amount = 0m;
            title = string.Empty;
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(raw))
            {
                title = "Invalid Amount";
                message = "Please enter a valid numeric amount.";
                return false;
            }

            var trimmed = raw.Trim();

            if (!decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
            {
                title = "Invalid Amount";
                message = "Please enter a valid numeric amount.";
                return false;
            }

            var unsigned = trimmed;
            if (unsigned.StartsWith("+", StringComparison.Ordinal) || unsigned.StartsWith("-", StringComparison.Ordinal))
                unsigned = unsigned.Substring(1);

            var parts = unsigned.Split('.');
            if (parts.Length > 2)
            {
                title = "Invalid Amount";
                message = "Please enter a valid numeric amount.";
                return false;
            }

            var digitsBeforeDecimal = parts[0].Length;
            if (digitsBeforeDecimal > 25)
            {
                title = "Invalid Amount";
                message = "Amount cannot exceed 25 digits before the decimal point.";
                return false;
            }

            var digitsAfterDecimal = parts.Length == 2 ? parts[1].Length : 0;
            if (digitsAfterDecimal > 8)
            {
                title = "Invalid Amount";
                message = "Amount cannot exceed 8 decimal places.";
                return false;
            }

            if (amount <= 0m)
            {
                title = "Incorrect Amount";
                message = "Amount must be greater than 0.";
                return false;
            }

            return true;
        }

        private static bool IsFormatValidXdcOr0x(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();

            if (s.StartsWith("xdc", StringComparison.OrdinalIgnoreCase))
                s = "0x" + s.Substring(3);

            if (!s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return false;
            if (s.Length != 42) return false;

            for (int i = 2; i < s.Length; i++)
            {
                char c = s[i];
                bool hex = (c >= '0' && c <= '9')
                           || (c >= 'a' && c <= 'f')
                           || (c >= 'A' && c <= 'F');
                if (!hex) return false;
            }

            return true;
        }

        private static string ToXdcAddress(string addr0xOrXdc)
        {
            var s = (addr0xOrXdc ?? "").Trim();

            if (s.StartsWith("xdc", StringComparison.OrdinalIgnoreCase))
                return "xdc" + s.Substring(3);

            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && s.Length == 42)
                return "xdc" + s.Substring(2);

            return s;
        }

        private static string ToCanonicalAddress(string addr0xOrXdc)
        {
            var s = (addr0xOrXdc ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(s))
                return string.Empty;

            if (s.StartsWith("xdc", StringComparison.OrdinalIgnoreCase))
                s = "0x" + s.Substring(3);

            return s.ToLowerInvariant();
        }

        private static string? TryGetFromAddress0x()
        {
            try
            {
                return AppSession.Instance.FromAddress0x;
            }
            catch
            {
                return null;
            }
        }

        private void AppendLine(string text)
        {
            OutputText.Text += (string.IsNullOrEmpty(OutputText.Text) ? "" : "\n") + text;
        }

        private static void SafeOpen(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                MessageBox.Show("Could not open your browser.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static async Task<decimal> GetBalanceXdcAsync(string rpcUrl, string address0x)
        {
            var weiHex = await RpcGas.GetBalanceWeiHexAsync(rpcUrl, address0x);

            if (string.IsNullOrWhiteSpace(weiHex))
                throw new InvalidOperationException("Balance result was empty.");

            if (weiHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                weiHex = weiHex.Substring(2);

            if (string.IsNullOrWhiteSpace(weiHex))
                return 0m;

            if (!BigInteger.TryParse("0" + weiHex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var wei))
                throw new InvalidOperationException("Balance result was not valid hex.");

            return UnitConversion.Convert.FromWei(wei);
        }
    }

    internal static class RpcGas
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        public static async Task<decimal?> GetGasPriceGweiAsync(string rpcUrl)
        {
            if (string.IsNullOrWhiteSpace(rpcUrl))
                rpcUrl = "https://xdc.public-rpc.com";

            var payload = new
            {
                jsonrpc = "2.0",
                method = "eth_gasPrice",
                @params = Array.Empty<object>(),
                id = 1
            };

            var json = JsonSerializer.Serialize(payload);

            using var resp = await _http.PostAsync(
                rpcUrl,
                new StringContent(json, Encoding.UTF8, "application/json"));

            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("result", out var resultEl))
                return null;

            var hex = resultEl.GetString();
            if (string.IsNullOrWhiteSpace(hex)) return null;

            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hex = hex[2..];

            if (!ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var wei))
                return null;

            return (decimal)wei / 1_000_000_000m;
        }

        public static async Task<string> GetBalanceWeiHexAsync(string rpcUrl, string address0x)
        {
            if (string.IsNullOrWhiteSpace(rpcUrl))
                rpcUrl = "https://xdc.public-rpc.com";

            var payload = new
            {
                jsonrpc = "2.0",
                method = "eth_getBalance",
                @params = new object[] { address0x, "latest" },
                id = 1
            };

            var json = JsonSerializer.Serialize(payload);

            using var resp = await _http.PostAsync(
                rpcUrl,
                new StringContent(json, Encoding.UTF8, "application/json"));

            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("result", out var resultEl))
                throw new InvalidOperationException("Balance result missing.");

            return resultEl.GetString() ?? "0x0";
        }
    }
}