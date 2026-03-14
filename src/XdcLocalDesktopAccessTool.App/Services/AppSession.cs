using System;

namespace XdcLocalDesktopAccessTool.App.Services
{
    public sealed class AppSession
    {
        private static readonly Lazy<AppSession> _instance =
            new Lazy<AppSession>(() => new AppSession());

        public static AppSession Instance => _instance.Value;

        private const string DefaultRpcUrl = "https://xdc.public-rpc.com";

        // XDC mainnet chainId (keeps it deterministic; later we can fetch eth_chainId if you want)
        public const int XdcChainId = 50;

        private string _currentRpcUrl;

        private AppSession()
        {
            _currentRpcUrl = DefaultRpcUrl;
        }

        // ======================================================
        // AUTHORISATION STATE + IN-MEMORY WALLET
        // ======================================================

        public bool IsAuthorised { get; private set; }

        // In-memory only. Never write to disk.
        public string? PrivateKeyHex { get; private set; }   // 0x-prefixed
        public string? FromAddress0x { get; private set; }   // 0x-prefixed

        public string? LastTxHash { get; set; }

        public void SetAuthorised()
        {
            IsAuthorised = true;
        }

        public void SetWallet(string privateKeyHex, string fromAddress0x)
        {
            PrivateKeyHex = privateKeyHex;
            FromAddress0x = fromAddress0x;
            IsAuthorised = true;
        }

        public void ClearAuthorisation()
        {
            IsAuthorised = false;
            LastTxHash = null;

            // Clear secrets deterministically
            PrivateKeyHex = null;
            FromAddress0x = null;
        }

        // ======================================================
        // GLOBAL RPC
        // ======================================================

        public string CurrentRpcUrl => _currentRpcUrl;

        public string GetDefaultRpc()
        {
            return DefaultRpcUrl;
        }

        public void SetRpc(string rpcUrl)
        {
            if (string.IsNullOrWhiteSpace(rpcUrl))
            {
                _currentRpcUrl = DefaultRpcUrl;
                return;
            }

            rpcUrl = rpcUrl.Trim();

            if (Uri.TryCreate(rpcUrl, UriKind.Absolute, out var uri)
                && (uri.Scheme == "https" || uri.Scheme == "http"))
            {
                _currentRpcUrl = rpcUrl;
            }
            else
            {
                _currentRpcUrl = DefaultRpcUrl;
            }
        }

        public void ResetRpcToDefault()
        {
            _currentRpcUrl = DefaultRpcUrl;
        }
    }
}