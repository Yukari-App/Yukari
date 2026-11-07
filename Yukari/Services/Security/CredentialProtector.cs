using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Yukari.Models.Data;
using Yukari.Services.Storage;

namespace Yukari.Services.Security;

internal class CredentialProtector : ICredentialProtector
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    internal const int DefaultPbkdf2Iterations = 600_000;
    private const string CanaryPlaintext = "yukari";

    private sealed record ProtectionState(CredentialProtectionConfig? Config, byte[]? Entropy);

    private readonly IDataService _dataService;
    private readonly ILogger<CredentialProtector> _logger;

    // Serializes protector state transitions and rotations (Initialize, Unlock, Enable, Change, Disable, Reset).
    // SourceSettingsStore.SaveSettingsAsync is invoked from a modal settings dialog that cannot overlap with the
    // modal credential protection dialog.
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    // Starts null (fail-closed) until InitializeAsync loads the configuration from the database.
    private volatile ProtectionState? _state;

    public bool UsesEntropy => _state?.Config != null;
    public bool IsUnlocked => _state is { } s && (s.Config == null || s.Entropy != null);

    public CredentialProtector(IDataService dataService, ILogger<CredentialProtector> logger)
    {
        _dataService = dataService;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_state is not null)
            return;

        await _semaphore.WaitAsync(ct);
        try
        {
            if (_state is not null)
                return;

            var config = await _dataService.GetCredentialProtectionAsync(ct);
            _state = new ProtectionState(config, null);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public byte[] Protect(string plainText)
    {
        ArgumentNullException.ThrowIfNull(plainText);

        var state = _state;
        if (state is null || (state.Config != null && state.Entropy == null))
            throw new InvalidOperationException("Credentials are locked.");

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        return ProtectedData.Protect(
            plainBytes,
            state.Config != null ? state.Entropy : null,
            DataProtectionScope.CurrentUser
        );
    }

    public string Unprotect(byte[] cipherText)
    {
        ArgumentNullException.ThrowIfNull(cipherText);

        var state = _state;
        if (state is null || (state.Config != null && state.Entropy == null))
            throw new InvalidOperationException("Credentials are locked.");

        var plainBytes = ProtectedData.Unprotect(
            cipherText,
            state.Config != null ? state.Entropy : null,
            DataProtectionScope.CurrentUser
        );
        return Encoding.UTF8.GetString(plainBytes);
    }

    public async Task<bool> UnlockAsync(string password, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var state = _state;
            if (state is null)
                return false;
            if (state.Config is null)
                return true;

            var entropy = await Task.Run(
                () => TryVerifyEntropy(password, state.Config, out var e) ? e : null,
                ct
            );
            if (entropy is null)
                return false;

            _state = state with { Entropy = entropy };
            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<int> EnableEntropyAsync(string newPassword, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(newPassword);

        await _semaphore.WaitAsync(ct);
        try
        {
            var state = _state;
            if (state is null)
                throw new InvalidOperationException("Credential protector is not initialized.");
            if (state.Config != null)
                throw new InvalidOperationException("Credential protection is already enabled.");

            var (newConfig, newEntropy) = await Task.Run(
                () => CreateConfigAndEntropy(newPassword),
                ct
            );

            var (reEncrypted, skippedCount) = await ReEncryptAllCredentialsAsync(
                null,
                newEntropy,
                ct
            );
            await ReplaceProtectionOrReloadLockedStateAsync(reEncrypted, newConfig, ct);

            _state = new ProtectionState(newConfig, newEntropy);
            return skippedCount;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<(bool Success, int SkippedCount)> ChangeEntropyPasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken ct = default
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(newPassword);

        await _semaphore.WaitAsync(ct);
        try
        {
            var state = _state;
            if (state?.Config is null)
                return (false, 0);

            var oldEntropy = await Task.Run(
                () => TryVerifyEntropy(currentPassword, state.Config, out var e) ? e : null,
                ct
            );
            if (oldEntropy is null)
                return (false, 0);

            var (newConfig, newEntropy) = await Task.Run(
                () => CreateConfigAndEntropy(newPassword),
                ct
            );

            var (reEncrypted, skippedCount) = await ReEncryptAllCredentialsAsync(
                oldEntropy,
                newEntropy,
                ct
            );
            await ReplaceProtectionOrReloadLockedStateAsync(reEncrypted, newConfig, ct);

            _state = new ProtectionState(newConfig, newEntropy);
            return (true, skippedCount);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<(bool Success, int SkippedCount)> DisableEntropyAsync(
        string currentPassword,
        CancellationToken ct = default
    )
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var state = _state;
            if (state?.Config is null)
                return (false, 0);

            var oldEntropy = await Task.Run(
                () => TryVerifyEntropy(currentPassword, state.Config, out var e) ? e : null,
                ct
            );
            if (oldEntropy is null)
                return (false, 0);

            var (reEncrypted, skippedCount) = await ReEncryptAllCredentialsAsync(
                oldEntropy,
                null,
                ct
            );
            await ReplaceProtectionOrReloadLockedStateAsync(reEncrypted, null, ct);

            _state = new ProtectionState(null, null);
            return (true, skippedCount);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ResetAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            await ReplaceProtectionOrReloadLockedStateAsync([], null, ct);
            _state = new ProtectionState(null, null);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task ReplaceProtectionOrReloadLockedStateAsync(
        IReadOnlyList<(string SourceName, string SettingKey, byte[] Ciphertext)> credentials,
        CredentialProtectionConfig? config,
        CancellationToken ct
    )
    {
        try
        {
            await _dataService.ReplaceCredentialProtectionAsync(credentials, config, ct);
        }
        catch
        {
            try
            {
                var persisted = await _dataService.GetCredentialProtectionAsync(
                    CancellationToken.None
                );
                _state = new ProtectionState(persisted, null);
            }
            catch (Exception reloadEx)
            {
                _logger.LogWarning(
                    reloadEx,
                    "Failed to reload credential protection state after a failed update; failing closed."
                );
                _state = null;
            }

            throw;
        }
    }

    private async Task<(
        List<(string SourceName, string SettingKey, byte[] Ciphertext)> ReEncrypted,
        int SkippedCount
    )> ReEncryptAllCredentialsAsync(byte[]? oldEntropy, byte[]? newEntropy, CancellationToken ct)
    {
        var existing = await _dataService.GetAllComicSourceCredentialsAsync(ct);
        var reEncrypted = new List<(string SourceName, string SettingKey, byte[] Ciphertext)>(
            existing.Count
        );
        var skippedCount = 0;

        foreach (var (sourceName, settingKey, ciphertext) in existing)
        {
            try
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
            catch (CryptographicException ex)
            {
                skippedCount++;
                _logger.LogWarning(
                    ex,
                    "Skipping undecryptable credential '{SettingKey}' for comic source '{SourceName}' while updating credential protection.",
                    settingKey,
                    sourceName
                );
            }
        }

        return (reEncrypted, skippedCount);
    }

    private static (CredentialProtectionConfig Config, byte[] Entropy) CreateConfigAndEntropy(
        string password
    )
    {
        var newSalt = RandomNumberGenerator.GetBytes(SaltSize);
        var newEntropy = DeriveEntropy(password, newSalt, DefaultPbkdf2Iterations);
        var canaryBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(CanaryPlaintext),
            newEntropy,
            DataProtectionScope.CurrentUser
        );
        var newConfig = new CredentialProtectionConfig(
            newSalt,
            canaryBytes,
            DefaultPbkdf2Iterations
        );
        return (newConfig, newEntropy);
    }

    private bool TryVerifyEntropy(
        string password,
        CredentialProtectionConfig config,
        out byte[]? derivedEntropy
    )
    {
        derivedEntropy = null;
        if (
            string.IsNullOrEmpty(password)
            || config.Salt is not { Length: > 0 }
            || config.Canary is not { Length: > 0 }
            || config.Iterations <= 0
        )
        {
            return false;
        }

        try
        {
            var candidate = DeriveEntropy(password, config.Salt, config.Iterations);

            var plainBytes = ProtectedData.Unprotect(
                config.Canary,
                candidate,
                DataProtectionScope.CurrentUser
            );
            if (Encoding.UTF8.GetString(plainBytes) != CanaryPlaintext)
                return false;

            derivedEntropy = candidate;
            return true;
        }
        catch (CryptographicException)
        {
            _logger.LogWarning("Failed to verify credential entropy password.");
            return false;
        }
    }

    private static byte[] DeriveEntropy(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, KeySize);
}
