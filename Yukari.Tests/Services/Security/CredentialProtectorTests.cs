using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Yukari.Models;
using Yukari.Models.Data;
using Yukari.Services.Security;
using Yukari.Services.Storage;

namespace Yukari.Tests.Services.Security;

public class CredentialProtectorTests
{
    private readonly Mock<IDataService> _dataServiceMock;
    private List<(string SourceName, string SettingKey, byte[] Ciphertext)> _storedCredentials;
    private CredentialProtectionConfig? _storedConfig;

    public CredentialProtectorTests()
    {
        _dataServiceMock = new Mock<IDataService>();
        _storedCredentials = [];
        _storedConfig = null;

        _dataServiceMock
            .Setup(d => d.GetAllComicSourceCredentialsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _storedCredentials);

        _dataServiceMock
            .Setup(d => d.GetCredentialProtectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _storedConfig);

        _dataServiceMock
            .Setup(d =>
                d.ReplaceCredentialProtectionAsync(
                    It.IsAny<
                        IReadOnlyList<(string SourceName, string SettingKey, byte[] Ciphertext)>
                    >(),
                    It.IsAny<CredentialProtectionConfig?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<
                IReadOnlyList<(string SourceName, string SettingKey, byte[] Ciphertext)>,
                CredentialProtectionConfig?,
                CancellationToken
            >(
                (creds, config, _) =>
                {
                    _storedCredentials = [.. creds];
                    _storedConfig = config;
                }
            )
            .Returns(Task.CompletedTask);
    }

    private CredentialProtector CreateSut() =>
        new(_dataServiceMock.Object, NullLogger<CredentialProtector>.Instance);

    [Fact]
    public async Task ProtectAndUnprotect_BeforeInitializeAsync_ThrowsInvalidOperationException()
    {
        // Arrange — uninitialized instance fails closed
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();

        // Act
        var protectAct = () => sut.Protect("secret");
        var unprotectAct = () => sut.Unprotect([1, 2, 3]);
        var enableAct = async () => await sut.EnableEntropyAsync("password", ct);
        var unlockResult = await sut.UnlockAsync("password", ct);

        // Assert
        sut.UsesEntropy.Should().BeFalse();
        sut.IsUnlocked.Should().BeFalse();
        protectAct.Should().Throw<InvalidOperationException>();
        unprotectAct.Should().Throw<InvalidOperationException>();
        await enableAct.Should().ThrowAsync<InvalidOperationException>();
        unlockResult.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_CalledTwiceAfterUnlock_DoesNotRelockOrReload()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = CreateSut();
        await seed.InitializeAsync(ct);
        await seed.EnableEntropyAsync("MasterPass!2026", ct);

        var sut = CreateSut();
        await sut.InitializeAsync(ct);
        (await sut.UnlockAsync("MasterPass!2026", ct)).Should().BeTrue();
        sut.IsUnlocked.Should().BeTrue();
        _dataServiceMock.Invocations.Clear();

        // Act
        await sut.InitializeAsync(ct);

        // Assert
        sut.UsesEntropy.Should().BeTrue();
        sut.IsUnlocked.Should().BeTrue();
        _dataServiceMock.Verify(
            d => d.GetCredentialProtectionAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ProtectAndUnprotect_WithoutEntropy_RoundTripsPlaintext()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        await sut.InitializeAsync(ct);
        const string secret = "mangadex-personal-token-123";

        // Act
        var ciphertext = sut.Protect(secret);
        var decrypted = sut.Unprotect(ciphertext);

        // Assert
        sut.UsesEntropy.Should().BeFalse();
        sut.IsUnlocked.Should().BeTrue();
        ciphertext.Should().NotBeEmpty();
        decrypted.Should().Be(secret);
    }

    [Fact]
    public async Task ProtectAndUnprotect_WithEntropy_RoundTripsAndFailsWithWrongEntropy()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        await sut.InitializeAsync(ct);
        await sut.EnableEntropyAsync("CorrectHorseBatteryStaple", ct);
        var ciphertext = sut.Protect("secret-token-xyz");

        var otherDataService = new Mock<IDataService>();
        otherDataService
            .Setup(d => d.GetAllComicSourceCredentialsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var wrongEntropyProtector = new CredentialProtector(
            otherDataService.Object,
            NullLogger<CredentialProtector>.Instance
        );
        await wrongEntropyProtector.InitializeAsync(ct);
        await wrongEntropyProtector.EnableEntropyAsync("DifferentPassword", ct);

        // Act
        var decrypted = sut.Unprotect(ciphertext);
        var wrongDecryptAct = () => wrongEntropyProtector.Unprotect(ciphertext);

        // Assert
        decrypted.Should().Be("secret-token-xyz");
        wrongDecryptAct.Should().Throw<CryptographicException>();
    }

    [Fact]
    public async Task Unprotect_WhenCiphertextIsTampered_ThrowsCryptographicException()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        await sut.InitializeAsync(ct);
        var ciphertext = sut.Protect("sensitive-api-key");
        ciphertext[^1] ^= 0xFF;

        // Act
        var act = () => sut.Unprotect(ciphertext);

        // Assert
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public async Task UnlockAsync_VerifiesCanaryAndUnlocksOnlyOnCorrectPassword()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var initialProtector = CreateSut();
        await initialProtector.InitializeAsync(ct);
        await initialProtector.EnableEntropyAsync("MasterPass!2026", ct);

        var newSessionProtector = CreateSut();
        await newSessionProtector.InitializeAsync(ct);

        // Act & Assert — starts locked in a new session when UsesEntropy is true
        newSessionProtector.UsesEntropy.Should().BeTrue();
        newSessionProtector.IsUnlocked.Should().BeFalse();

        (await newSessionProtector.UnlockAsync("WrongPass", ct)).Should().BeFalse();
        newSessionProtector.IsUnlocked.Should().BeFalse();

        (await newSessionProtector.UnlockAsync("MasterPass!2026", ct)).Should().BeTrue();
        newSessionProtector.IsUnlocked.Should().BeTrue();
    }

    [Fact]
    public async Task EnableChangeAndDisableEntropy_ReEncryptsAllStoredCredentialsPreservingValues()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        await sut.InitializeAsync(ct);
        _storedCredentials =
        [
            ("MangaDex", "api_token", sut.Protect("token-alpha")),
            ("OtherSource", "secret_key", sut.Protect("token-beta")),
        ];

        // Act 1: Enable entropy password
        var skippedOnEnable = await sut.EnableEntropyAsync("FirstPassword123", ct);

        // Assert 1: All credentials re-encrypted with FirstPassword123 and 600,000 PBKDF2 iterations
        skippedOnEnable.Should().Be(0);
        sut.UsesEntropy.Should().BeTrue();
        sut.IsUnlocked.Should().BeTrue();
        _storedConfig.Should().NotBeNull();
        _storedConfig!.Iterations.Should().Be(CredentialProtector.DefaultPbkdf2Iterations);
        _storedCredentials.Should().HaveCount(2);
        sut.Unprotect(_storedCredentials[0].Ciphertext).Should().Be("token-alpha");
        sut.Unprotect(_storedCredentials[1].Ciphertext).Should().Be("token-beta");

        // Act 2: Change entropy password
        var (changed, skippedOnChange) = await sut.ChangeEntropyPasswordAsync(
            "FirstPassword123",
            "SecondPassword456",
            ct
        );
        changed.Should().BeTrue();
        skippedOnChange.Should().Be(0);

        // Assert 2: Re-encrypted with SecondPassword456 and unlockable in a fresh session
        var freshSession = CreateSut();
        await freshSession.InitializeAsync(ct);
        (await freshSession.UnlockAsync("FirstPassword123", ct)).Should().BeFalse();
        (await freshSession.UnlockAsync("SecondPassword456", ct)).Should().BeTrue();
        freshSession.Unprotect(_storedCredentials[0].Ciphertext).Should().Be("token-alpha");
        freshSession.Unprotect(_storedCredentials[1].Ciphertext).Should().Be("token-beta");

        // Act 3: Disable entropy password
        var (disabled, skippedOnDisable) = await freshSession.DisableEntropyAsync(
            "SecondPassword456",
            ct
        );
        disabled.Should().BeTrue();
        skippedOnDisable.Should().Be(0);

        // Assert 3: Back to DPAPI without custom entropy, all values intact, config cleared
        freshSession.UsesEntropy.Should().BeFalse();
        freshSession.IsUnlocked.Should().BeTrue();
        _storedConfig.Should().BeNull();
        freshSession.Unprotect(_storedCredentials[0].Ciphertext).Should().Be("token-alpha");
        freshSession.Unprotect(_storedCredentials[1].Ciphertext).Should().Be("token-beta");
    }

    [Fact]
    public async Task EnableEntropyAsync_WhenAlreadyEnabled_ThrowsInvalidOperationException()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        await sut.InitializeAsync(ct);
        await sut.EnableEntropyAsync("FirstPassword123", ct);

        // Act
        var act = async () => await sut.EnableEntropyAsync("BypassPassword", ct);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ChangeAndDisableEntropy_WithWrongPassword_MakesZeroCallsToDataService()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        await sut.InitializeAsync(ct);
        await sut.EnableEntropyAsync("CorrectPassword", ct);
        _dataServiceMock.Invocations.Clear();

        // Act
        var (changeResult, _) = await sut.ChangeEntropyPasswordAsync(
            "WrongPassword",
            "NewPassword",
            ct
        );
        var (disableResult, _) = await sut.DisableEntropyAsync("WrongPassword", ct);

        // Assert
        changeResult.Should().BeFalse();
        disableResult.Should().BeFalse();
        _dataServiceMock.Verify(
            d => d.GetAllComicSourceCredentialsAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
        _dataServiceMock.Verify(
            d =>
                d.ReplaceCredentialProtectionAsync(
                    It.IsAny<
                        IReadOnlyList<(string SourceName, string SettingKey, byte[] Ciphertext)>
                    >(),
                    It.IsAny<CredentialProtectionConfig?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task ReplaceCredentialProtectionFailure_ReloadsPersistedConfigAndFailsClosed()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        await sut.InitializeAsync(ct);
        await sut.EnableEntropyAsync("InitialPassword", ct);
        var validCiphertext = sut.Protect("my-secret");
        var originalConfig = _storedConfig;

        _dataServiceMock
            .Setup(d =>
                d.ReplaceCredentialProtectionAsync(
                    It.IsAny<
                        IReadOnlyList<(string SourceName, string SettingKey, byte[] Ciphertext)>
                    >(),
                    It.IsAny<CredentialProtectionConfig?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("Simulated database write failure"));

        // Act — pre-commit failure: database still has originalConfig, state reloads as locked
        var changeAct = async () =>
            await sut.ChangeEntropyPasswordAsync("InitialPassword", "NewPassword", ct);

        // Assert
        await changeAct.Should().ThrowAsync<InvalidOperationException>();

        sut.UsesEntropy.Should().BeTrue();
        sut.IsUnlocked.Should().BeFalse();
        _storedConfig.Should().BeSameAs(originalConfig);

        // Re-unlocking with the persisted password works and decrypts existing ciphertext
        (await sut.UnlockAsync("InitialPassword", ct))
            .Should()
            .BeTrue();
        sut.Unprotect(validCiphertext).Should().Be("my-secret");

        // Simulate post-commit exception (database persisted new config, then threw on dispose/cancel)
        _dataServiceMock
            .Setup(d =>
                d.ReplaceCredentialProtectionAsync(
                    It.IsAny<
                        IReadOnlyList<(string SourceName, string SettingKey, byte[] Ciphertext)>
                    >(),
                    It.IsAny<CredentialProtectionConfig?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<
                IReadOnlyList<(string SourceName, string SettingKey, byte[] Ciphertext)>,
                CredentialProtectionConfig?,
                CancellationToken
            >(
                (creds, config, _) =>
                {
                    _storedCredentials = [.. creds];
                    _storedConfig = config;
                }
            )
            .ThrowsAsync(new IOException("Post-commit connection close error"));

        var postCommitAct = async () =>
            await sut.ChangeEntropyPasswordAsync("InitialPassword", "CommittedNewPassword", ct);
        await postCommitAct.Should().ThrowAsync<IOException>();

        // State reloaded the newly committed config in locked state, so unlocking with CommittedNewPassword succeeds
        sut.UsesEntropy.Should().BeTrue();
        sut.IsUnlocked.Should().BeFalse();
        (await sut.UnlockAsync("InitialPassword", ct)).Should().BeFalse();
        (await sut.UnlockAsync("CommittedNewPassword", ct)).Should().BeTrue();
    }

    [Fact]
    public async Task EnableEntropyAsync_WithUndecryptableBlob_SkipsCorruptedBlobAndReturnsSkippedCount()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        await sut.InitializeAsync(ct);
        _storedCredentials =
        [
            ("MangaDex", "valid_token", sut.Protect("healthy-secret")),
            ("BrokenSource", "corrupted_token", [0x01, 0x02, 0x03, 0x04]),
        ];

        // Act
        var skippedCount = await sut.EnableEntropyAsync("MasterPass123", ct);

        // Assert
        skippedCount.Should().Be(1);
        sut.UsesEntropy.Should().BeTrue();
        sut.IsUnlocked.Should().BeTrue();
        _storedCredentials.Should().ContainSingle();
        _storedCredentials[0].SourceName.Should().Be("MangaDex");
        sut.Unprotect(_storedCredentials[0].Ciphertext).Should().Be("healthy-secret");
    }

    [Fact]
    public async Task ResetAsync_WithUndecryptableBlobs_ClearsAllCredentialsAndDisablesProtection()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        await sut.InitializeAsync(ct);
        await sut.EnableEntropyAsync("MasterPass123", ct);

        var lockedSession = CreateSut();
        await lockedSession.InitializeAsync(ct);
        _storedCredentials = [("BrokenSource", "corrupted_token", [0xDE, 0xAD, 0xBE, 0xEF])];

        // Act
        await lockedSession.ResetAsync(ct);

        // Assert
        lockedSession.UsesEntropy.Should().BeFalse();
        lockedSession.IsUnlocked.Should().BeTrue();
        _storedCredentials.Should().BeEmpty();
        _storedConfig.Should().BeNull();
    }

    [Fact]
    public async Task UnlockAsync_ConcurrentlyWithChangeEntropyPasswordAsync_SerializesAndKeepsStateConsistent()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        await sut.InitializeAsync(ct);
        await sut.EnableEntropyAsync("OldPass", ct);
        _storedCredentials = [("MangaDex", "api_token", sut.Protect("secret-value"))];

        // Start a fresh locked session against the same config & credentials
        var session = CreateSut();
        await session.InitializeAsync(ct);

        // Act — launch ChangeEntropyPasswordAsync and UnlockAsync(OldPass) in parallel
        var changeTask = Task.Run(
            () => session.ChangeEntropyPasswordAsync("OldPass", "NewPass", ct),
            ct
        );
        var unlockTask = Task.Run(() => session.UnlockAsync("OldPass", ct), ct);

        await Task.WhenAll(changeTask, unlockTask);

        // Assert — Change succeeded, and regardless of ordering, the final state in memory matches
        // the re-encrypted credential in _storedCredentials (no stale entropy/config overwrite).
        (await changeTask)
            .Success.Should()
            .BeTrue();
        session.UsesEntropy.Should().BeTrue();
        session.IsUnlocked.Should().BeTrue();
        session.Unprotect(_storedCredentials[0].Ciphertext).Should().Be("secret-value");
    }

    [Fact]
    public async Task DataService_ReplaceCredentialProtectionAsync_WithRealSqlite_RollsBackBothTablesOnMidTransactionFailure()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var tempDbPath = Path.Combine(
            Path.GetTempPath(),
            $"yukari_cred_atomic_{Guid.NewGuid():N}.db"
        );
        var connectionString = $"Data Source={tempDbPath};Pooling=False";

        try
        {
            var migrator = new DatabaseMigrator(
                NullLogger<DatabaseMigrator>.Instance,
                connectionString
            );
            await migrator.MigrateAsync();

            var dataService = new DataService(connectionString);
            await dataService.UpsertComicSourceAsync(
                new ComicSourceModel
                {
                    Name = "MangaDex",
                    Version = "2.6.0",
                    DllPath = @"C:\Plugins\MangaDex.dll",
                    IsEnabled = true,
                    IsConfigurable = true,
                }
            );

            var initialConfig = new CredentialProtectionConfig([1, 2, 3, 4], [5, 6, 7, 8], 600_000);
            var initialCredentials = new List<(
                string SourceName,
                string SettingKey,
                byte[] Ciphertext
            )>
            {
                ("MangaDex", "api_token", [10, 20, 30]),
            };

            await dataService.ReplaceCredentialProtectionAsync(
                initialCredentials,
                initialConfig,
                ct
            );

            // Act — pass a config with a null Salt that violates NOT NULL on CredentialProtection
            // AFTER ComicSourceCredentials has already been deleted and re-inserted inside the transaction.
            var newCredentials = new List<(string SourceName, string SettingKey, byte[] Ciphertext)>
            {
                ("MangaDex", "api_token", [99, 99, 99]),
            };
            var invalidConfig = new CredentialProtectionConfig(null!, [9, 9, 9], 600_000);

            var failingAct = async () =>
                await dataService.ReplaceCredentialProtectionAsync(
                    newCredentials,
                    invalidConfig,
                    ct
                );

            // Assert — SQLite rejects the second table write and rolls back the first table write too
            await failingAct.Should().ThrowAsync<SqliteException>();

            var persistedCreds = await dataService.GetAllComicSourceCredentialsAsync(ct);
            var persistedConfig = await dataService.GetCredentialProtectionAsync(ct);

            persistedCreds.Should().ContainSingle();
            persistedCreds[0].Ciphertext.Should().Equal([10, 20, 30]);

            persistedConfig.Should().NotBeNull();
            persistedConfig!.Salt.Should().Equal([1, 2, 3, 4]);
            persistedConfig.Canary.Should().Equal([5, 6, 7, 8]);
            persistedConfig.Iterations.Should().Be(600_000);
        }
        finally
        {
            if (File.Exists(tempDbPath))
                File.Delete(tempDbPath);
        }
    }
}
