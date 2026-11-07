using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using Yukari.Enums;
using Yukari.Services.Security;
using Yukari.Services.Sources;
using Yukari.Services.UI;
using Yukari.ViewModels.Dialogs;

namespace Yukari.Tests.ViewModels.Dialogs;

public class CredentialProtectionDialogViewModelTests
{
    private readonly Mock<ICredentialProtector> _credentialProtectorMock;
    private readonly Mock<ISourceService> _sourceServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<ILocalizationService> _localizationServiceMock;
    private readonly CredentialProtectionDialogViewModel _sut;

    public CredentialProtectionDialogViewModelTests()
    {
        _credentialProtectorMock = new Mock<ICredentialProtector>();
        _sourceServiceMock = new Mock<ISourceService>();
        _notificationServiceMock = new Mock<INotificationService>();
        _localizationServiceMock = new Mock<ILocalizationService>();

        _localizationServiceMock
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns<string>(k => k);

        _sut = new CredentialProtectionDialogViewModel(
            _credentialProtectorMock.Object,
            _sourceServiceMock.Object,
            _notificationServiceMock.Object,
            _localizationServiceMock.Object
        );
    }

    [Fact]
    public async Task SubmitAsync_WhenUnlockSucceeds_ReAppliesSettingsToAllLoadedSources()
    {
        // Arrange
        _sut.Initialize(CredentialProtectionMode.Unlock);
        _sut.CurrentPassword = "secret";
        _credentialProtectorMock.Setup(c => c.TryUnlock("secret")).Returns(true);

        // Act
        var result = await _sut.SubmitAsync();

        // Assert
        result.Should().BeTrue();
        _sut.HasError.Should().BeFalse();
        _sourceServiceMock.Verify(
            s => s.ApplySettingsToAllLoadedSourcesAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
        _notificationServiceMock.Verify(
            n => n.ShowSuccess("SuccessCredentialsUnlocked", It.IsAny<string>()),
            Times.Once
        );
    }

    [Fact]
    public async Task SubmitAsync_WhenUnlockFails_SetsErrorMessageAndDoesNotReApply()
    {
        // Arrange
        _sut.Initialize(CredentialProtectionMode.Unlock);
        _sut.CurrentPassword = "wrong";
        _credentialProtectorMock.Setup(c => c.TryUnlock("wrong")).Returns(false);

        // Act
        var result = await _sut.SubmitAsync();

        // Assert
        result.Should().BeFalse();
        _sut.HasError.Should().BeTrue();
        _sut.ErrorMessage.Should().Be("ErrorIncorrectCredentialPassword");
        _sourceServiceMock.Verify(
            s => s.ApplySettingsToAllLoadedSourcesAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task SubmitAsync_WhenEnablePasswordsMismatch_SetsErrorMessage()
    {
        // Arrange
        _sut.Initialize(CredentialProtectionMode.Enable);
        _sut.NewPassword = "pass1";
        _sut.ConfirmPassword = "pass2";

        // Act
        var result = await _sut.SubmitAsync();

        // Assert
        result.Should().BeFalse();
        _sut.ErrorMessage.Should().Be("ErrorCredentialPasswordMismatch");
        _credentialProtectorMock.Verify(
            c => c.EnableEntropyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task SubmitAsync_WhenEnableSucceeds_CallsEnableEntropyAsync()
    {
        // Arrange
        _sut.Initialize(CredentialProtectionMode.Enable);
        _sut.NewPassword = "pass1";
        _sut.ConfirmPassword = "pass1";

        // Act
        var result = await _sut.SubmitAsync();

        // Assert
        result.Should().BeTrue();
        _credentialProtectorMock.Verify(
            c => c.EnableEntropyAsync("pass1", It.IsAny<CancellationToken>()),
            Times.Once
        );
        _notificationServiceMock.Verify(
            n => n.ShowSuccess("SuccessCredentialProtectionEnabled", It.IsAny<string>()),
            Times.Once
        );
    }

    [Fact]
    public async Task SwitchToResetMode_ThenSubmitAsync_ResetsProtectionAndReAppliesSettings()
    {
        // Arrange
        _sut.Initialize(CredentialProtectionMode.Unlock);
        _sut.CurrentPassword = "wrong";
        _credentialProtectorMock.Setup(c => c.TryUnlock("wrong")).Returns(false);
        await _sut.SubmitAsync();
        _sut.HasError.Should().BeTrue();
        _sut.CanSwitchToReset.Should().BeTrue();

        // Act
        _sut.SwitchToResetModeCommand.Execute(null);

        // Assert mode transition
        _sut.Mode.Should().Be(CredentialProtectionMode.Reset);
        _sut.IsResetMode.Should().BeTrue();
        _sut.CanSwitchToReset.Should().BeFalse();
        _sut.ShowCurrentPassword.Should().BeFalse();
        _sut.HasError.Should().BeFalse();

        // Act submit reset
        var result = await _sut.SubmitAsync();

        // Assert reset execution
        result.Should().BeTrue();
        _credentialProtectorMock.Verify(
            c => c.ResetAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
        _sourceServiceMock.Verify(
            s => s.ApplySettingsToAllLoadedSourcesAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
        _notificationServiceMock.Verify(
            n => n.ShowSuccess("SuccessCredentialProtectionReset", It.IsAny<string>()),
            Times.Once
        );
    }
}
