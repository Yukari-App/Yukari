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

    bool TryUnlock(string password);

    Task EnableEntropyAsync(string newPassword, CancellationToken ct = default);
    Task<bool> ChangeEntropyPasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken ct = default
    );
    Task<bool> DisableEntropyAsync(string currentPassword, CancellationToken ct = default);
    Task ResetAsync(CancellationToken ct = default);
}
