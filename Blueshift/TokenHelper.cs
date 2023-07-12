namespace Blueshift
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    public class TokenHelper
    {
        private static readonly byte[] entropyBytes = { 0xde, 0xad, 0xbe, 0xef };

#pragma warning disable CA1416 // Validate platform compatibility
        internal static string Protect(string value)
        {
            byte[] rawData = Encoding.UTF8.GetBytes(value);
            byte[] encData = ProtectedData.Protect(rawData, entropyBytes, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encData);
        }

        internal static string Unprotect(string value)
        {
            byte[] encData = Convert.FromBase64String(value);
            byte[] rawData = ProtectedData.Unprotect(encData, entropyBytes, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(rawData);
        }
#pragma warning restore CA1416 // Validate platform compatibility
    }
}
