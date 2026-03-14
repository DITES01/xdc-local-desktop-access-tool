using System;
using System.Text.RegularExpressions;

namespace XdcLocalDesktopAccessTool.App.Services
{
    public static class ValidationService
    {
        public static bool IsEmpty(string? value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        public static bool IsValidXdcAddress(string? address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return false;

            // Basic XDC / Ethereum-style check (0x or xdc prefix, 40 hex chars)
            var pattern = @"^(0x|xdc)[0-9a-fA-F]{40}$";
            return Regex.IsMatch(address.Trim(), pattern);
        }

        public static bool IsPositiveDecimal(string? value)
        {
            if (decimal.TryParse(value, out var result))
                return result > 0;

            return false;
        }

        public static bool IsValidUrl(string? url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out _);
        }
    }
}
