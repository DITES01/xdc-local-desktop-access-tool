using System;
using System.Text.RegularExpressions;

namespace XdcLocalDesktopAccessTool.App.Services
{
    internal static class AddressFormat
    {
        private static readonly Regex Hex40 = new Regex("^[0-9a-fA-F]{40}$", RegexOptions.Compiled);

        public static string ToXdcAddress(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            var s = input.Trim();

            if (s.StartsWith("xdc", StringComparison.OrdinalIgnoreCase))
            {
                var hex = s.Substring(3);
                return "xdc" + hex;
            }

            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                var hex = s.Substring(2);
                return "xdc" + hex;
            }

            return s;
        }

        public static bool IsValidXdcOr0x(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            var s = input.Trim();

            if (s.StartsWith("xdc", StringComparison.OrdinalIgnoreCase))
            {
                if (s.Length != 43) return false;
                var hex = s.Substring(3);
                return Hex40.IsMatch(hex);
            }

            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (s.Length != 42) return false;
                var hex = s.Substring(2);
                return Hex40.IsMatch(hex);
            }

            return false;
        }
    }
}
