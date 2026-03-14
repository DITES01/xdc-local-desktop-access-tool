using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Nethereum.Signer;

namespace XdcLocalDesktopAccessTool.App.Services
{
    public static class WalletDerivationService
    {
        public sealed record WalletDerivationResult(
            IReadOnlyList<string> MnemonicWords,
            string PrivateKeyHex0x,
            string Address0x,
            string AddressXdc,
            string DerivationPathUsed);

        public sealed record DerivedAccountResult(
            int Index,
            string PrivateKeyHex0x,
            string Address0x,
            string AddressXdc,
            string DerivationPathUsed);

        // =========================================================
        // NEW WALLET (create mnemonic + derive single account)
        // =========================================================
        public static WalletDerivationResult GenerateNewWallet(int wordCount, string? bip39Passphrase, string derivationPath)
        {
            if (wordCount != 12 && wordCount != 24)
                throw new ArgumentException("Word count must be 12 or 24.", nameof(wordCount));

            if (string.IsNullOrWhiteSpace(derivationPath))
                throw new ArgumentException("Derivation path is required.", nameof(derivationPath));

            var wc = wordCount == 12 ? WordCount.Twelve : WordCount.TwentyFour;

            // 1) Create mnemonic (BIP39)
            var mnemonic = new Mnemonic(Wordlist.English, wc);
            var words = mnemonic.Words; // string[]

            // 2) Master key from mnemonic + optional BIP39 passphrase
            var passphrase = bip39Passphrase ?? string.Empty;
            var master = mnemonic.DeriveExtKey(passphrase);

            // 3) Derive along the requested path (BIP32/BIP44)
            var resolvedPath = ResolveDerivationPath(derivationPath, 0);
            var keyPath = ToNBitcoinKeyPath(resolvedPath);
            var derived = master.Derive(keyPath);

            // 4) Private key bytes -> 0x private key hex (in-memory only)
            var privBytes = derived.PrivateKey.ToBytes();
            var privateKey0x = "0x" + BitConverter.ToString(privBytes).Replace("-", "").ToLowerInvariant();

            // 5) Ethereum-style address from secp256k1 key (0x-prefixed)
            var ecKey = new EthECKey(privBytes, true);
            var address0x = ecKey.GetPublicAddress();

            // 6) XDC display format
            var addressXdc = AddressFormat.ToXdcAddress(address0x);

            // Best-effort wipe
            Array.Clear(privBytes, 0, privBytes.Length);

            return new WalletDerivationResult(
                MnemonicWords: words,
                PrivateKeyHex0x: privateKey0x,
                Address0x: address0x,
                AddressXdc: addressXdc,
                DerivationPathUsed: resolvedPath);
        }

        // =========================================================
        // EXISTING MNEMONIC (derive account at index)
        // - For scanning derived addresses and letting user select one
        // =========================================================
        public static DerivedAccountResult DeriveFromMnemonic(
            string mnemonicPhrase,
            string? bip39Passphrase,
            string derivationPathTemplate,
            int index)
        {
            if (string.IsNullOrWhiteSpace(mnemonicPhrase))
                throw new ArgumentException("Mnemonic is required.", nameof(mnemonicPhrase));

            if (string.IsNullOrWhiteSpace(derivationPathTemplate))
                throw new ArgumentException("Derivation path is required.", nameof(derivationPathTemplate));

            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            var mnemonic = new Mnemonic(mnemonicPhrase.Trim(), Wordlist.English);

            var passphrase = bip39Passphrase ?? string.Empty;
            var master = mnemonic.DeriveExtKey(passphrase);

            var resolvedPath = ResolveDerivationPath(derivationPathTemplate, index);
            var keyPath = ToNBitcoinKeyPath(resolvedPath);
            var derived = master.Derive(keyPath);

            var privBytes = derived.PrivateKey.ToBytes();
            var privateKey0x = "0x" + BitConverter.ToString(privBytes).Replace("-", "").ToLowerInvariant();

            var ecKey = new EthECKey(privBytes, true);
            var address0x = ecKey.GetPublicAddress();
            var addressXdc = AddressFormat.ToXdcAddress(address0x);

            Array.Clear(privBytes, 0, privBytes.Length);

            return new DerivedAccountResult(
                Index: index,
                PrivateKeyHex0x: privateKey0x,
                Address0x: address0x,
                AddressXdc: addressXdc,
                DerivationPathUsed: resolvedPath);
        }

        public static IReadOnlyList<DerivedAccountResult> DeriveManyFromMnemonic(
            string mnemonicPhrase,
            string? bip39Passphrase,
            string derivationPathTemplate,
            int count)
        {
            if (count <= 0) return Array.Empty<DerivedAccountResult>();

            var list = new List<DerivedAccountResult>(count);
            for (int i = 0; i < count; i++)
                list.Add(DeriveFromMnemonic(mnemonicPhrase, bip39Passphrase, derivationPathTemplate, i));

            return list;
        }

        // =========================================================
        // Helpers
        // =========================================================
        private static string ResolveDerivationPath(string template, int index)
        {
            var p = (template ?? string.Empty).Trim();

            // Supports:
            //  - "m/44'/550'/0'/0/{i}"
            //  - "m/44'/550'/0'/0/i"
            //  - "m/44'/550'/0'/0/0" (no placeholder -> treated as fixed path)
            if (p.Contains("{i}", StringComparison.OrdinalIgnoreCase))
                return p.Replace("{i}", index.ToString(), StringComparison.OrdinalIgnoreCase);

            if (p.EndsWith("/i", StringComparison.OrdinalIgnoreCase))
                return p.Substring(0, p.Length - 2) + "/" + index;

            return p;
        }

        private static KeyPath ToNBitcoinKeyPath(string derivationPath)
        {
            var p = derivationPath.Trim();
            if (p.StartsWith("m/", StringComparison.OrdinalIgnoreCase))
                p = p.Substring(2);

            return new KeyPath(p);
        }
    }
}