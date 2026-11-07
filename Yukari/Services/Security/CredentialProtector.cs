using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Yukari.Services.Settings;
using Yukari.Services.Storage;

namespace Yukari.Services.Security;

internal class CredentialProtector : ICredentialProtector
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Pbkdf2Iterations = 210_000;
    private const string CanaryPlaintext = "yukari";

    private readonly ISettingsService _settingsService;
    private readonly IDataService _dataService;
    private readonly ILogger<CredentialProtector>? _logger;

    private byte[]? _entropy;

    public bool UsesEntropy => _settingsService.Current.UsesCredentialEntropy;
    public bool IsUnlocked => !UsesEntropy || _entropy != null;

    public CredentialProtector(
        ISettingsService settingsService,
        IDataService dataService,
        ILogger<CredentialProtector>? logger = null
    )
    {
        _settingsService = settingsService;
        _dataService = dataService;
        _logger = logger;
    }

    public byte[] Protect(string plainText)
    {
        ArgumentNullException.ThrowIfNull(plainText);

        if (UsesEntropy && _entropy == null)
            throw new InvalidOperationException("Credentials are locked.");

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        return ProtectedData.Protect(
            plainBytes,
            UsesEntropy ? _entropy : null,
            DataProtectionScope.CurrentUser
        );
    }

    public string Unprotect(byte[] cipherText)
    {
        ArgumentNullException.ThrowIfNull(cipherText);

        if (UsesEntropy && _entropy == null)
            throw new InvalidOperationException("Credentials are locked.");

        var plainBytes = ProtectedData.Unprotect(
            cipherText,
            UsesEntropy ? _entropy : null,
            DataProtectionScope.CurrentUser
        );
        return Encoding.UTF8.GetString(plainBytes);
    }

    public bool TryUnlock(string password)
    {
        if (!UsesEntropy)
            return true;

        if (!TryVerifyEntropy(password, out var derivedEntropy))
            return false;

        _entropy = derivedEntropy;
        return true;
    }

    public async Task EnableEntropyAsync(string newPassword, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(newPassword);

        byte[]? currentEntropy = null;
        if (UsesEntropy)
        {
            if (_entropy == null)
                throw new InvalidOperationException("Credentials are locked.");
            currentEntropy = _entropy;
        }

        var newSalt = RandomNumberGenerator.GetBytes(SaltSize);
        var newEntropy = DeriveEntropy(newPassword, newSalt);

        await ReEncryptAllCredentialsAsync(currentEntropy, newEntropy, ct);

        var canaryBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(CanaryPlaintext),
            newEntropy,
            DataProtectionScope.CurrentUser
        );

        _settingsService.Set(s => s.UsesCredentialEntropy, true);
        _settingsService.Set(s => s.CredentialEntropySalt, Convert.ToBase64String(newSalt));
        _settingsService.Set(s => s.CredentialEntropyCanary, Convert.ToBase64String(canaryBytes));
        await _settingsService.SaveAsync();

        _entropy = newEntropy;
    }

    public async Task<bool> ChangeEntropyPasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken ct = default
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(newPassword);

        if (!TryVerifyEntropy(currentPassword, out var oldEntropy))
            return false;

        var newSalt = RandomNumberGenerator.GetBytes(SaltSize);
        var newEntropy = DeriveEntropy(newPassword, newSalt);

        await ReEncryptAllCredentialsAsync(oldEntropy, newEntropy, ct);

        var canaryBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(CanaryPlaintext),
            newEntropy,
            DataProtectionScope.CurrentUser
        );

        _settingsService.Set(s => s.UsesCredentialEntropy, true);
        _settingsService.Set(s => s.CredentialEntropySalt, Convert.ToBase64String(newSalt));
        _settingsService.Set(s => s.CredentialEntropyCanary, Convert.ToBase64String(canaryBytes));
        await _settingsService.SaveAsync();

        _entropy = newEntropy;
        return true;
    }

    public async Task<bool> DisableEntropyAsync(
        string currentPassword,
        CancellationToken ct = default
    )
    {
        if (!TryVerifyEntropy(currentPassword, out var oldEntropy))
            return false;

        await ReEncryptAllCredentialsAsync(oldEntropy, null, ct);

        _settingsService.Set(s => s.UsesCredentialEntropy, false);
        _settingsService.Set(s => s.CredentialEntropySalt, null);
        _settingsService.Set(s => s.CredentialEntropyCanary, null);
        await _settingsService.SaveAsync();

        _entropy = null;
        return true;
    }

    private async Task ReEncryptAllCredentialsAsync(
        byte[]? oldEntropy,
        byte[]? newEntropy,
        CancellationToken ct
    )
    {
        var existing = await _dataService.GetAllComicSourceCredentialsAsync(ct);
        var reEncrypted = new List<(string SourceName, string SettingKey, byte[] Ciphertext)>(
            existing.Count
        );

        foreach (var (sourceName, settingKey, ciphertext) in existing)
        {
            var plainBytes = ProtectedData.Unprotect(
                ciphertext,
                oldEntropy,
                DataProtectionScope.CurrentUser
            );
            var newCiphertext = ProtectedData.Protect(
                plainBytes,
                newEntropy,
                DataProtectionScope.CurrentUser
            );
            reEncrypted.Add((sourceName, settingKey, newCiphertext));
        }

        await _dataService.ReplaceAllComicSourceCredentialsAsync(reEncrypted);
    }

    private bool TryVerifyEntropy(string password, out byte[]? derivedEntropy)
    {
        derivedEntropy = null;
        if (string.IsNullOrEmpty(password))
            return false;

        var saltBase64 = _settingsService.Current.CredentialEntropySalt;
        var canaryBase64 = _settingsService.Current.CredentialEntropyCanary;

        if (string.IsNullOrEmpty(saltBase64) || string.IsNullOrEmpty(canaryBase64))
            return false;

        try
        {
            var salt = Convert.FromBase64String(saltBase64);
            var canaryBytes = Convert.FromBase64String(canaryBase64);
            var candidate = DeriveEntropy(password, salt);

            var plainBytes = ProtectedData.Unprotect(
                canaryBytes,
                candidate,
                DataProtectionScope.CurrentUser
            );
            if (Encoding.UTF8.GetString(plainBytes) != CanaryPlaintext)
                return false;

            derivedEntropy = candidate;
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            _logger?.LogWarning("Failed to verify credential entropy password.");
            return false;
        }
    }

    private static byte[] DeriveEntropy(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            KeySize
        );
}
