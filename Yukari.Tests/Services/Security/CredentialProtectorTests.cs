using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using Yukari.Models.Settings;
using Yukari.Services.Security;
using Yukari.Services.Settings;
using Yukari.Services.Storage;

namespace Yukari.Tests.Services.Security;

public class CredentialProtectorTests
{
    private readonly AppSettings _appSettings;
    private readonly Mock<ISettingsService> _settingsServiceMock;
    private readonly Mock<IDataService> _dataServiceMock;
    private List<(string SourceName, string SettingKey, byte[] Ciphertext)> _storedCredentials;

    public CredentialProtectorTests()
    {
        _appSettings = new AppSettings();
        _settingsServiceMock = new Mock<ISettingsService>();
        _dataServiceMock = new Mock<IDataService>();
        _storedCredentials = [];

        _settingsServiceMock.Setup(s => s.Current).Returns(_appSettings);
        _settingsServiceMock
            .Setup(s => s.Set(It.IsAny<Expression<Func<AppSettings, bool>>>(), It.IsAny<bool>()))
            .Callback<Expression<Func<AppSettings, bool>>, bool>(SetProperty);
        _settingsServiceMock
            .Setup(s =>
                s.Set(It.IsAny<Expression<Func<AppSettings, string?>>>(), It.IsAny<string?>())
            )
            .Callback<Expression<Func<AppSettings, string?>>, string?>(SetProperty);

        _dataServiceMock
            .Setup(d => d.GetAllComicSourceCredentialsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _storedCredentials);

        _dataServiceMock
            .Setup(d =>
                d.ReplaceAllComicSourceCredentialsAsync(
                    It.IsAny<
                        IReadOnlyList<(string SourceName, string SettingKey, byte[] Ciphertext)>
                    >()
                )
            )
            .Callback<IReadOnlyList<(string SourceName, string SettingKey, byte[] Ciphertext)>>(
                creds => _storedCredentials = [.. creds]
            )
            .Returns(Task.CompletedTask);
    }

    private CredentialProtector CreateSut() =>
        new(_settingsServiceMock.Object, _dataServiceMock.Object);

    private void SetProperty<T>(Expression<Func<AppSettings, T>> selector, T value)
    {
        if (selector.Body is MemberExpression { Member: PropertyInfo prop })
            prop.SetValue(_appSettings, value);
    }

    [Fact]
    public void ProtectAndUnprotect_WithoutEntropy_RoundTripsPlaintext()
    {
        // Arrange
        var sut = CreateSut();
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
        await sut.EnableEntropyAsync("CorrectHorseBatteryStaple", ct);
        var ciphertext = sut.Protect("secret-token-xyz");

        var otherSettings = new AppSettings();
        var otherSettingsService = new Mock<ISettingsService>();
        otherSettingsService.Setup(s => s.Current).Returns(otherSettings);
        otherSettingsService
            .Setup(s => s.Set(It.IsAny<Expression<Func<AppSettings, bool>>>(), It.IsAny<bool>()))
            .Callback<Expression<Func<AppSettings, bool>>, bool>(
                (expr, val) =>
                {
                    if (expr.Body is MemberExpression { Member: PropertyInfo prop })
                        prop.SetValue(otherSettings, val);
                }
            );
        otherSettingsService
            .Setup(s =>
                s.Set(It.IsAny<Expression<Func<AppSettings, string?>>>(), It.IsAny<string?>())
            )
            .Callback<Expression<Func<AppSettings, string?>>, string?>(
                (expr, val) =>
                {
                    if (expr.Body is MemberExpression { Member: PropertyInfo prop })
                        prop.SetValue(otherSettings, val);
                }
            );

        var wrongEntropyProtector = new CredentialProtector(
            otherSettingsService.Object,
            _dataServiceMock.Object
        );
        await wrongEntropyProtector.EnableEntropyAsync("DifferentPassword", ct);

        // Act
        var decrypted = sut.Unprotect(ciphertext);
        var wrongDecryptAct = () => wrongEntropyProtector.Unprotect(ciphertext);

        // Assert
        decrypted.Should().Be("secret-token-xyz");
        wrongDecryptAct.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Unprotect_WhenCiphertextIsTampered_ThrowsCryptographicException()
    {
        // Arrange
        var sut = CreateSut();
        var ciphertext = sut.Protect("sensitive-api-key");
        ciphertext[^1] ^= 0xFF;

        // Act
        var act = () => sut.Unprotect(ciphertext);

        // Assert
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public async Task TryUnlock_VerifiesCanaryAndUnlocksOnlyOnCorrectPassword()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var initialProtector = CreateSut();
        await initialProtector.EnableEntropyAsync("MasterPass!2026", ct);

        var newSessionProtector = CreateSut();

        // Act & Assert — starts locked in a new session when UsesEntropy is true
        newSessionProtector.UsesEntropy.Should().BeTrue();
        newSessionProtector.IsUnlocked.Should().BeFalse();

        newSessionProtector.TryUnlock("WrongPass").Should().BeFalse();
        newSessionProtector.IsUnlocked.Should().BeFalse();

        newSessionProtector.TryUnlock("MasterPass!2026").Should().BeTrue();
        newSessionProtector.IsUnlocked.Should().BeTrue();
    }

    [Fact]
    public async Task EnableChangeAndDisableEntropy_ReEncryptsAllStoredCredentialsPreservingValues()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        _storedCredentials =
        [
            ("MangaDex", "api_token", sut.Protect("token-alpha")),
            ("OtherSource", "secret_key", sut.Protect("token-beta")),
        ];

        // Act 1: Enable entropy password
        await sut.EnableEntropyAsync("FirstPassword123", ct);

        // Assert 1: All credentials re-encrypted with FirstPassword123
        sut.UsesEntropy.Should().BeTrue();
        sut.IsUnlocked.Should().BeTrue();
        _storedCredentials.Should().HaveCount(2);
        sut.Unprotect(_storedCredentials[0].Ciphertext).Should().Be("token-alpha");
        sut.Unprotect(_storedCredentials[1].Ciphertext).Should().Be("token-beta");

        // Act 2: Change entropy password (wrong current password fails, right current password succeeds)
        var failedChange = await sut.ChangeEntropyPasswordAsync(
            "WrongCurrent",
            "SecondPassword456",
            ct
        );
        failedChange.Should().BeFalse();

        var changed = await sut.ChangeEntropyPasswordAsync(
            "FirstPassword123",
            "SecondPassword456",
            ct
        );
        changed.Should().BeTrue();

        // Assert 2: Re-encrypted with SecondPassword456 and unlockable in a fresh session
        var freshSession = CreateSut();
        freshSession.TryUnlock("FirstPassword123").Should().BeFalse();
        freshSession.TryUnlock("SecondPassword456").Should().BeTrue();
        freshSession.Unprotect(_storedCredentials[0].Ciphertext).Should().Be("token-alpha");
        freshSession.Unprotect(_storedCredentials[1].Ciphertext).Should().Be("token-beta");

        // Act 3: Disable entropy password
        var failedDisable = await freshSession.DisableEntropyAsync("WrongPassword", ct);
        failedDisable.Should().BeFalse();

        var disabled = await freshSession.DisableEntropyAsync("SecondPassword456", ct);
        disabled.Should().BeTrue();

        // Assert 3: Back to DPAPI without custom entropy, all values intact
        freshSession.UsesEntropy.Should().BeFalse();
        freshSession.IsUnlocked.Should().BeTrue();
        freshSession.Unprotect(_storedCredentials[0].Ciphertext).Should().Be("token-alpha");
        freshSession.Unprotect(_storedCredentials[1].Ciphertext).Should().Be("token-beta");
    }
}
