using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using Moq;
using Yukari.Enums;
using Yukari.Models;
using Yukari.Models.Common;
using Yukari.Models.Settings;
using Yukari.Services.Comics;
using Yukari.Services.Settings;
using Yukari.Services.UI;
using Yukari.Tests.TestUtils;
using Yukari.ViewModels.Components;
using Yukari.ViewModels.Pages;

namespace Yukari.Tests.ViewModels.Pages;

public class SettingsPageViewModelTests
{
    private readonly Mock<ISettingsService> _settingsServiceMock;
    private readonly Mock<IComicService> _comicServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly IMessenger _messenger;
    private readonly Mock<ILocalizationService> _localizationServiceMock;

    private readonly SettingsPageViewModel _sut;

    public SettingsPageViewModelTests()
    {
        _settingsServiceMock = new Mock<ISettingsService>();
        _comicServiceMock = new Mock<IComicService>();
        _notificationServiceMock = new Mock<INotificationService>();
        _dialogServiceMock = new Mock<IDialogService>();
        _messenger = new FakeMessenger();
        _localizationServiceMock = new Mock<ILocalizationService>();

        _settingsServiceMock.Setup(s => s.Current).Returns(new AppSettings());
        _comicServiceMock
            .Setup(s => s.GetComicSourcesAsync())
            .ReturnsAsync(
                Result<IReadOnlyList<ComicSourceModel>>.Success(new List<ComicSourceModel>())
            );

        _sut = new SettingsPageViewModel(
            _settingsServiceMock.Object,
            _comicServiceMock.Object,
            _notificationServiceMock.Object,
            _dialogServiceMock.Object,
            _messenger,
            _localizationServiceMock.Object
        );
    }

    // ────────────────────────────────────────────────────────────────
    // PROPERTIES
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void AvailableThemeModes_ShouldContainAllEnumValues()
    {
        _sut.AvailableThemeModes.Should().BeEquivalentTo(Enum.GetValues<ThemeMode>());
    }

    [Fact]
    public void AvailableReadingModes_ShouldContainAllEnumValues()
    {
        _sut.AvailableReadingModes.Should().BeEquivalentTo(Enum.GetValues<ReadingMode>());
    }

    [Fact]
    public void AvailableScalingModes_ShouldContainAllEnumValues()
    {
        _sut.AvailableScalingModes.Should().BeEquivalentTo(Enum.GetValues<ScalingMode>());
    }

    [Fact]
    public void YukariVersion_ShouldNotBeNullOrEmpty()
    {
        _sut.YukariVersion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CoreVersion_ShouldNotBeNullOrEmpty()
    {
        _sut.CoreVersion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void IsAnyComicSourceEnabled_ShouldBeFalse_WhenNoEnabledSources()
    {
        _sut.AvailableComicSources = new();
        _sut.IsAnyComicSourceEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsAnyComicSourceEnabled_ShouldBeTrue_WhenEnabledSourcesExist()
    {
        _sut.AvailableComicSources = new()
        {
            new ComicSourceModel
            {
                Name = "Test",
                DllPath = ".dll",
                Version = "1",
                IsEnabled = true,
            },
        };
        _sut.IsAnyComicSourceEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsComicSourcesEmpty_ShouldBeTrue_WhenNoItems()
    {
        _sut.ComicSourceItems = new();
        _sut.IsComicSourcesEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsComicSourcesEmpty_ShouldBeFalse_WhenItemsExist()
    {
        _sut.ComicSourceItems = new()
        {
            new ComicSourceItemViewModel(
                new ComicSourceModel
                {
                    Name = "Test",
                    DllPath = ".dll",
                    Version = "1",
                },
                null!,
                null!
            ),
        };
        _sut.IsComicSourcesEmpty.Should().BeFalse();
    }

    // ────────────────────────────────────────────────────────────────
    // PUBLIC METHODS
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OnNavigatedFromAsync_ShouldSaveSettings()
    {
        // Act
        await _sut.OnNavigatedFromAsync();

        // Assert
        _settingsServiceMock.Verify(s => s.SaveAsync(), Times.Once);
    }

    // ────────────────────────────────────────────────────────────────
    // COMMANDS EXECUTION
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddComicSource_AddsSource_AndReloadsSources_WhenSuccess()
    {
        // Arrange
        var pluginPath = @"C:\plugins\TestSource.dll";
        _dialogServiceMock
            .Setup(d => d.OpenFilePickerAsync(It.IsAny<string[]>()))
            .ReturnsAsync(pluginPath);
        _comicServiceMock
            .Setup(s => s.UpsertComicSourceAsync(pluginPath))
            .ReturnsAsync(Result.Success());
        _comicServiceMock
            .Setup(s => s.GetComicSourcesAsync())
            .ReturnsAsync(
                Result<IReadOnlyList<ComicSourceModel>>.Success(
                    new List<ComicSourceModel>
                    {
                        new ComicSourceModel
                        {
                            Name = "TestSource",
                            DllPath = pluginPath,
                            Version = "1",
                            IsEnabled = true,
                        },
                    }
                )
            );

        // Act
        await _sut.AddComicSourceCommand.ExecuteAsync(null);

        // Assert
        _comicServiceMock.Verify(s => s.UpsertComicSourceAsync(pluginPath), Times.Once);
        _notificationServiceMock.Verify(
            n => n.ShowSuccess(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
        _sut.AvailableComicSources.Should().HaveCount(1);
        _sut.ComicSourceItems.Should().HaveCount(1);
        _sut.DefaultComicSource.Should().NotBeNull();
    }

    [Fact]
    public async Task AddComicSource_ShowsWarning_WhenPendingRestart()
    {
        // Arrange
        var pluginPath = @"C:\plugins\TestSource.dll";
        _dialogServiceMock
            .Setup(d => d.OpenFilePickerAsync(It.IsAny<string[]>()))
            .ReturnsAsync(pluginPath);
        _comicServiceMock
            .Setup(s => s.UpsertComicSourceAsync(pluginPath))
            .ReturnsAsync(Result.PendingRestart());

        // Act
        await _sut.AddComicSourceCommand.ExecuteAsync(null);

        // Assert
        _notificationServiceMock.Verify(
            n => n.ShowWarning(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
    }

    [Fact]
    public async Task AddComicSource_ShowsError_WhenFailure()
    {
        // Arrange
        var pluginPath = @"C:\plugins\TestSource.dll";
        _dialogServiceMock
            .Setup(d => d.OpenFilePickerAsync(It.IsAny<string[]>()))
            .ReturnsAsync(pluginPath);
        _comicServiceMock
            .Setup(s => s.UpsertComicSourceAsync(pluginPath))
            .ReturnsAsync(Result.Failure("Error", "Title"));

        // Act
        await _sut.AddComicSourceCommand.ExecuteAsync(null);

        // Assert
        _notificationServiceMock.Verify(n => n.ShowError("Error", "Title"), Times.Once);
    }

    [Fact]
    public async Task RemoveComicSource_RemovesSource_AndReloadsSources_WhenSuccess()
    {
        // Arrange
        var itemVm = new ComicSourceItemViewModel(
            new ComicSourceModel
            {
                Name = "Test",
                DllPath = ".dll",
                Version = "1",
            },
            null!,
            null!
        );
        _comicServiceMock
            .Setup(s => s.RemoveComicSourceAsync("Test"))
            .ReturnsAsync(Result.Success());
        _comicServiceMock
            .Setup(s => s.GetComicSourcesAsync())
            .ReturnsAsync(
                Result<IReadOnlyList<ComicSourceModel>>.Success(new List<ComicSourceModel>())
            );

        // Act
        await _sut.RemoveComicSourceCommand.ExecuteAsync(itemVm);

        // Assert
        _comicServiceMock.Verify(s => s.RemoveComicSourceAsync("Test"), Times.Once);
        _sut.ComicSourceItems.Should().BeEmpty();
    }

    [Fact]
    public async Task OnComicSourceIsEnabledChanged_UpdatesSource_AndReloadsAvailableSources()
    {
        // Arrange
        var settings = new AppSettings { DefaultComicSourceName = "Test" };
        _settingsServiceMock.Setup(s => s.Current).Returns(settings);

        var source = new ComicSourceModel
        {
            Name = "Test",
            DllPath = ".dll",
            Version = "1",
            IsEnabled = true,
        };
        _sut.ComicSourceItems = new() { new ComicSourceItemViewModel(source, null!, null!) };
        _comicServiceMock
            .Setup(s => s.UpdateComicSourceIsEnabledAsync("Test", true))
            .ReturnsAsync(Result.Success());

        // Act
        await _sut.ComicSourceIsEnabledChangedCommand.ExecuteAsync(("Test", true));

        // Assert
        _comicServiceMock.Verify(s => s.UpdateComicSourceIsEnabledAsync("Test", true), Times.Once);
        _sut.AvailableComicSources.Should().ContainSingle(c => c.Name == "Test");
        _sut.DefaultComicSource.Should().NotBeNull();
    }

    [Fact]
    public async Task CleanUpStorage_CleansUp_WhenSuccess()
    {
        // Arrange
        _comicServiceMock
            .Setup(s => s.CleanupUnfavoriteComicsDataAsync())
            .ReturnsAsync(Result.Success());

        // Act
        await _sut.CleanUpStorageCommand.ExecuteAsync(null);

        // Assert
        _comicServiceMock.Verify(s => s.CleanupUnfavoriteComicsDataAsync(), Times.Once);
        _notificationServiceMock.Verify(
            n => n.ShowSuccess(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
    }

    [Fact]
    public async Task CleanUpStorage_ShowsError_WhenFailure()
    {
        // Arrange
        _comicServiceMock
            .Setup(s => s.CleanupUnfavoriteComicsDataAsync())
            .ReturnsAsync(Result.Failure("DB error", "Error"));

        // Act
        await _sut.CleanUpStorageCommand.ExecuteAsync(null);

        // Assert
        _notificationServiceMock.Verify(n => n.ShowError("DB error", "Error"), Times.Once);
    }
}
