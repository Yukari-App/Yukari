using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Yukari.Services.Security;

internal class CredentialProtector : ICredentialProtector
{
    private readonly ILogger<CredentialProtector>? _logger;

    public CredentialProtector(ILogger<CredentialProtector>? logger = null)
    {
        _logger = logger;
    }

    public byte[] Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return [];

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            return ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException ex)
        {
            _logger?.LogError(ex, "Failed to encrypt credential.");
            return [];
        }
    }

    public string Decrypt(byte[] cipherText)
    {
        if (cipherText == null || cipherText.Length == 0)
            return string.Empty;

        try
        {
            var plainBytes = ProtectedData.Unprotect(cipherText, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException ex)
        {
            _logger?.LogWarning(ex, "Failed to decrypt credential. Falling back to empty string.");
            return string.Empty;
        }
    }
}
