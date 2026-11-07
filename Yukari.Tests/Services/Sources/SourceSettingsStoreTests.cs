using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Yukari.Core.Models.Settings;
using Yukari.Services.Security;
using Yukari.Services.Sources;
using Yukari.Services.Storage;

namespace Yukari.Tests.Services.Sources;

public class SourceSettingsStoreTests
{
    private readonly Mock<IDataService> _dataServiceMock;
    private readonly Mock<ICredentialProtector> _credentialProtectorMock;
    private readonly SourceSettingsStore _store;

    public SourceSettingsStoreTests()
    {
        _dataServiceMock = new Mock<IDataService>();
        _credentialProtectorMock = new Mock<ICredentialProtector>();
        _credentialProtectorMock.Setup(p => p.IsUnlocked).Returns(true);
        _store = new SourceSettingsStore(
            _dataServiceMock.Object,
            _credentialProtectorMock.Object,
            NullLogger<SourceSettingsStore>.Instance
        );
    }

    [Fact]
    public async Task LoadRawValuesAsync_MergesPlainSettingsAndDecryptedCredentials()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        const string sourceName = "TestPlugin";
        var schema = new List<SourceSetting>
        {
            new ToggleSetting
            {
                Key = "enable_cache",
                Header = "Enable Cache",
                DefaultValue = true,
            },
            new PasswordSetting { Key = "api_token", Header = "API Token" },
        };

        var plainSettings = new Dictionary<string, string> { ["enable_cache"] = "false" };
        var cipherBytes = Encoding.UTF8.GetBytes("encrypted-token");
        var credentials = new Dictionary<string, byte[]> { ["api_token"] = cipherBytes };

        _dataServiceMock
            .Setup(d => d.GetComicSourceSettingsAsync(sourceName, ct))
            .ReturnsAsync(plainSettings);

        _dataServiceMock
            .Setup(d => d.GetComicSourceCredentialsAsync(sourceName, ct))
            .ReturnsAsync(credentials);

        _credentialProtectorMock
            .Setup(p => p.Unprotect(cipherBytes))
            .Returns("decrypted-token-123");

        // Act
        var result = await _store.LoadRawValuesAsync(sourceName, schema, ct);

        // Assert
        result.Should().ContainKey("enable_cache").WhoseValue.Should().Be("false");
        result.Should().ContainKey("api_token").WhoseValue.Should().Be("decrypted-token-123");
    }

    [Fact]
    public async Task SaveSettingsAsync_SplitsPlainSettingsAndEncryptedCredentials_IgnoresUndeclaredKeys()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        const string sourceName = "TestPlugin";
        var schema = new List<SourceSetting>
        {
            new TextSetting { Key = "server_url", Header = "Server URL" },
            new PasswordSetting { Key = "auth_key", Header = "Auth Key" },
        };

        var valuesToSave = new Dictionary<string, string>
        {
            ["server_url"] = "https://example.com",
            ["auth_key"] = "my_secret_key",
            ["stale_key"] = "should_be_ignored",
        };

        var expectedCipher = new byte[] { 1, 2, 3, 4 };
        _credentialProtectorMock.Setup(p => p.Protect("my_secret_key")).Returns(expectedCipher);

        IReadOnlyDictionary<string, string>? capturedPlain = null;
        IReadOnlyDictionary<string, byte[]>? capturedCreds = null;

        _dataServiceMock
            .Setup(d =>
                d.SaveComicSourceSettingsAsync(
                    sourceName,
                    It.IsAny<IReadOnlyDictionary<string, string>>()
                )
            )
            .Callback<string, IReadOnlyDictionary<string, string>>((_, p) => capturedPlain = p)
            .Returns(Task.CompletedTask);

        _dataServiceMock
            .Setup(d =>
                d.SaveComicSourceCredentialsAsync(
                    sourceName,
                    It.IsAny<IReadOnlyDictionary<string, byte[]>>()
                )
            )
            .Callback<string, IReadOnlyDictionary<string, byte[]>>((_, c) => capturedCreds = c)
            .Returns(Task.CompletedTask);

        // Act
        await _store.SaveSettingsAsync(sourceName, schema, valuesToSave, ct);

        // Assert
        capturedPlain.Should().NotBeNull();
        capturedPlain
            .Should()
            .ContainKey("server_url")
            .WhoseValue.Should()
            .Be("https://example.com");
        capturedPlain.Should().NotContainKey("stale_key");
        capturedPlain.Should().NotContainKey("auth_key");

        capturedCreds.Should().NotBeNull();
        capturedCreds
            .Should()
            .ContainKey("auth_key")
            .WhoseValue.Should()
            .BeEquivalentTo(expectedCipher);
        capturedCreds.Should().NotContainKey("stale_key");
        capturedCreds.Should().NotContainKey("server_url");
    }

    [Fact]
    public async Task LoadSettingsAsync_ReturnsConfiguredSourceSettingsInstance()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        const string sourceName = "TestPlugin";
        var schema = new List<SourceSetting>
        {
            new ToggleSetting
            {
                Key = "debug_mode",
                Header = "Debug",
                DefaultValue = false,
            },
        };

        _dataServiceMock
            .Setup(d => d.GetComicSourceSettingsAsync(sourceName, ct))
            .ReturnsAsync(new Dictionary<string, string> { ["debug_mode"] = "true" });

        // Act
        var settings = await _store.LoadSettingsAsync(sourceName, schema, ct);

        // Assert
        settings.Should().NotBeNull();
        settings.GetBool("debug_mode").Should().BeTrue();
    }

    [Fact]
    public async Task SaveSettingsAsync_PreservesUntouchedPassword_AndRemovesClearedPassword()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        const string sourceName = "TestPlugin";
        var schema = new List<SourceSetting>
        {
            new PasswordSetting { Key = "kept_token", Header = "Kept Token" },
            new PasswordSetting { Key = "cleared_token", Header = "Cleared Token" },
        };

        var existingKeptCipher = new byte[] { 10, 20, 30 };
        var existingClearedCipher = new byte[] { 40, 50, 60 };
        _dataServiceMock
            .Setup(d => d.GetComicSourceCredentialsAsync(sourceName, ct))
            .ReturnsAsync(
                new Dictionary<string, byte[]>
                {
                    ["kept_token"] = existingKeptCipher,
                    ["cleared_token"] = existingClearedCipher,
                }
            );

        // kept_token is omitted (unedited), cleared_token is empty string (cleared)
        var valuesToSave = new Dictionary<string, string> { ["cleared_token"] = string.Empty };

        IReadOnlyDictionary<string, byte[]>? capturedCreds = null;
        _dataServiceMock
            .Setup(d =>
                d.SaveComicSourceCredentialsAsync(
                    sourceName,
                    It.IsAny<IReadOnlyDictionary<string, byte[]>>()
                )
            )
            .Callback<string, IReadOnlyDictionary<string, byte[]>>((_, c) => capturedCreds = c)
            .Returns(Task.CompletedTask);

        // Act
        await _store.SaveSettingsAsync(sourceName, schema, valuesToSave, ct);

        // Assert
        capturedCreds.Should().NotBeNull();
        capturedCreds
            .Should()
            .ContainKey("kept_token")
            .WhoseValue.Should()
            .BeEquivalentTo(existingKeptCipher);
        capturedCreds.Should().NotContainKey("cleared_token");
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenCredentialsAreLocked_OmitsCredentialsAndReadsEmptyDefault()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        const string sourceName = "TestPlugin";
        var schema = new List<SourceSetting>
        {
            new PasswordSetting { Key = "api_token", Header = "API Token" },
        };

        _credentialProtectorMock.Setup(p => p.IsUnlocked).Returns(false);
        _dataServiceMock
            .Setup(d => d.GetComicSourceSettingsAsync(sourceName, ct))
            .ReturnsAsync(new Dictionary<string, string>());

        // Act
        var settings = await _store.LoadSettingsAsync(sourceName, schema, ct);

        // Assert
        settings.GetString("api_token").Should().BeEmpty();
        _dataServiceMock.Verify(
            d =>
                d.GetComicSourceCredentialsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
