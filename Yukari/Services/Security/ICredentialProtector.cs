using System.Threading;
using System.Threading.Tasks;

namespace Yukari.Services.Security;

public interface ICredentialProtector
{
    bool UsesEntropy { get; }
    bool IsUnlocked { get; }

    Task InitializeAsync(CancellationToken ct = default);

    byte[] Protect(string plainText);
    string Unprotect(byte[] cipherText);

    Task<bool> UnlockAsync(string password, CancellationToken ct = default);

    Task<int> EnableEntropyAsync(string newPassword, CancellationToken ct = default);
    Task<(bool Success, int SkippedCount)> ChangeEntropyPasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken ct = default
    );
    Task<(bool Success, int SkippedCount)> DisableEntropyAsync(
        string currentPassword,
        CancellationToken ct = default
    );
    Task ResetAsync(CancellationToken ct = default);
}
