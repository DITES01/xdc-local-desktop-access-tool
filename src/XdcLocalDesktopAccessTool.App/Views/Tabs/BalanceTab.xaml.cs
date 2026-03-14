using System;
using System.Globalization;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using XdcLocalDesktopAccessTool.App.Services;

namespace XdcLocalDesktopAccessTool.App.Views.Tabs
{
    public partial class BalanceTab : UserControl
    {
        private const string DefaultRpcUrl = "https://xdc.public-rpc.com";

        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        private static readonly Regex HexOnly =
            new Regex("^[0-9a-fA-F]+$", RegexOptions.Compiled);

        private bool _uiReady;
        private bool _isBusy;

        public BalanceTab()
        {
            InitializeComponent();

            if (CustomRpcTextBox != null)
                CustomRpcTextBox.Text = DefaultRpcUrl;

            Loaded += (_, __) =>
            {
                _uiReady = true;
                ApplyRpcModeUi();
                ApplyRpcToSession();
                UpdateAddressValidationUi();
            };
        }

        private void RpcMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_uiReady) return;
            ApplyRpcModeUi();
            ApplyRpcToSession();
        }

        private void ApplyRpcModeUi()
        {
            if (RpcModeComboBox == null || CustomRpcTextBox == null)
                return;

            bool isCustom = RpcModeComboBox.SelectedIndex == 1;
            CustomRpcTextBox.IsEnabled = isCustom;

            if (!isCustom)
                CustomRpcTextBox.Text = DefaultRpcUrl;
        }

        private void ApplyRpcToSession()
        {
            if (RpcModeComboBox == null)
            {
                AppSession.Instance.ResetRpcToDefault();
                return;
            }

            if (RpcModeComboBox.SelectedIndex != 1)
            {
                AppSession.Instance.ResetRpcToDefault();
                return;
            }

            var custom = (CustomRpcTextBox?.Text ?? "").Trim();
            AppSession.Instance.SetRpc(custom);
        }

        private bool ValidateCustomRpcOrConfirmDefaultFallback()
        {
            if (RpcModeComboBox == null || CustomRpcTextBox == null)
                return true;

            bool isCustom = RpcModeComboBox.SelectedIndex == 1;
            if (!isCustom)
                return true;

            var custom = (CustomRpcTextBox.Text ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(custom))
                return true;

            var result = MessageBox.Show(
                "Custom RPC is selected, but the RPC field is blank.\n\nDo you want to continue using the default RPC instead?",
                "Blank Custom RPC",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return false;

            RpcModeComboBox.SelectedIndex = 0;
            CustomRpcTextBox.Text = DefaultRpcUrl;
            ApplyRpcModeUi();
            ApplyRpcToSession();
            return true;
        }

        private string GetRpcUrlForRequest()
        {
            return AppSession.Instance.CurrentRpcUrl;
        }

        private async void CheckBalance_Click(object sender, RoutedEventArgs e)
        {
            BalanceTextBlock.Text = "";

            var input = (AddressTextBox.Text ?? "").Trim();
            if (!TryValidateAndNormalizeAddress(input, out var addr0x, out var addrXdc))
                return;

            if (!ValidateCustomRpcOrConfirmDefaultFallback())
                return;

            var rpcUrl = GetRpcUrlForRequest();

            SetBusy(true);

            try
            {
                var chainId = await TryGetChainIdAsync(rpcUrl);
                var weiHex = await GetBalanceWeiHexAsync(rpcUrl, addr0x);

                if (!TryHexToBigInteger(weiHex, out var wei))
                {
                    ShowError("Received unexpected response from network.");
                    return;
                }

                var xdc = WeiToXdcString(wei);

                var sb = new StringBuilder();
                sb.AppendLine($"Address (xdc): {addrXdc}");
                sb.AppendLine($"Address (0x):  {addr0x}");
                sb.AppendLine($"RPC:           {rpcUrl}");
                if (chainId != null)
                    sb.AppendLine($"ChainId:       {chainId}");
                sb.AppendLine($"Balance:       {xdc} XDC");

                BalanceTextBlock.Text = sb.ToString().TrimEnd();
            }
            catch
            {
                ShowError("Unable to reach the XDC network.");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void ViewOnExplorer_Click(object sender, RoutedEventArgs e)
        {
            var input = (AddressTextBox.Text ?? "").Trim();
            if (!TryValidateAndNormalizeAddress(input, out var addr0x, out _))
                return;

            var url = $"https://xdcscan.com/address/{addr0x}";

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                ShowError("Could not open explorer.");
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            ClearForTabChange();
        }

        public void ClearForTabChange()
        {
            AddressTextBox.Text = "";
            BalanceTextBlock.Text = "";
            UpdateAddressValidationUi();
        }

        private void AddressTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_uiReady) return;
            UpdateAddressValidationUi();
        }

        private void UpdateAddressValidationUi()
        {
            bool isValid = IsAddressFormatValid(AddressTextBox?.Text);
            bool hasText = !string.IsNullOrWhiteSpace(AddressTextBox?.Text);

            if (AddressValidTick != null)
                AddressValidTick.Visibility = isValid ? Visibility.Visible : Visibility.Hidden;

            if (ViewOnExplorerButton != null)
                ViewOnExplorerButton.IsEnabled = isValid && !_isBusy;

            if (ClearButton != null)
                ClearButton.IsEnabled = hasText && !_isBusy;
        }

        private bool IsAddressFormatValid(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var s = input.Trim();

            if (s.StartsWith("xdc", StringComparison.OrdinalIgnoreCase))
                s = "0x" + s.Substring(3);

            if (!s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return false;

            if (s.Length != 42)
                return false;

            var hex = s.Substring(2);
            return HexOnly.IsMatch(hex);
        }

        private bool TryValidateAndNormalizeAddress(string input, out string addr0x, out string addrXdc)
        {
            addr0x = "";
            addrXdc = "";

            if (string.IsNullOrWhiteSpace(input))
            {
                ShowError("Please enter an address.");
                return false;
            }

            var s = input.Trim();

            if (s.StartsWith("xdc", StringComparison.OrdinalIgnoreCase))
                s = "0x" + s.Substring(3);

            if (s.Length != 42)
            {
                ShowError("Invalid address.");
                return false;
            }

            var hex = s.Substring(2);
            if (!HexOnly.IsMatch(hex))
            {
                ShowError("Invalid address.");
                return false;
            }

            addr0x = "0x" + hex;
            addrXdc = "xdc" + hex;
            return true;
        }

        private static async Task<string> GetBalanceWeiHexAsync(string rpcUrl, string addr0x)
        {
            var payload = new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "eth_getBalance",
                @params = new object[] { addr0x, "latest" }
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var resp = await Http.PostAsync(rpcUrl, content);
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("result", out var result))
                return result.GetString() ?? "0x0";

            return "0x0";
        }

        private static async Task<string?> TryGetChainIdAsync(string rpcUrl)
        {
            try
            {
                var payload = new
                {
                    jsonrpc = "2.0",
                    id = 1,
                    method = "eth_chainId",
                    @params = Array.Empty<object>()
                };

                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var resp = await Http.PostAsync(rpcUrl, content);
                resp.EnsureSuccessStatusCode();

                var body = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);

                if (!doc.RootElement.TryGetProperty("result", out var result))
                    return null;

                var chainIdHex = result.GetString();
                if (chainIdHex == null) return null;

                if (chainIdHex.StartsWith("0x"))
                    chainIdHex = chainIdHex.Substring(2);

                if (!BigInteger.TryParse(chainIdHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bi))
                    return null;

                return bi.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static bool TryHexToBigInteger(string hex, out BigInteger value)
        {
            value = BigInteger.Zero;

            if (string.IsNullOrWhiteSpace(hex))
                return false;

            if (hex.StartsWith("0x"))
                hex = hex.Substring(2);

            return BigInteger.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        private static string WeiToXdcString(BigInteger wei)
        {
            const int chainDecimals = 18;
            const int displayDecimals = 9;

            var divisor = BigInteger.Pow(10, chainDecimals);
            var whole = BigInteger.DivRem(wei, divisor, out var remainder);

            var scaleDown = BigInteger.Pow(10, chainDecimals - displayDecimals);
            var frac = remainder / scaleDown;

            var wholeStr = whole.ToString(CultureInfo.InvariantCulture);
            var fracStr = frac.ToString(CultureInfo.InvariantCulture).PadLeft(displayDecimals, '0');

            return $"{wholeStr}.{fracStr}";
        }

        private static void ShowError(string message)
        {
            MessageBox.Show(message, "XDC Desktop Access Tool",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void SetBusy(bool isBusy)
        {
            _isBusy = isBusy;

            CheckBalanceButton.IsEnabled = !isBusy;
            AddressTextBox.IsEnabled = !isBusy;
            RpcModeComboBox.IsEnabled = !isBusy;
            CustomRpcTextBox.IsEnabled =
                !isBusy && RpcModeComboBox.SelectedIndex == 1;

            UpdateAddressValidationUi();
        }
    }
}