using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Yukari.Core.Models.Settings;
using Yukari.Models.Common;
using Yukari.Services.Sources;
using Yukari.Services.UI;
using Yukari.ViewModels.Components.Settings;
using Yukari.ViewModels.Dialogs;

namespace Yukari.Tests.ViewModels.Dialogs;

public class ComicSourceSettingsDialogViewModelTests
{
    private sealed record CustomUnknownSetting : SourceSetting;

    private readonly Mock<ISourceManager> _sourceManagerMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<ILocalizationService> _localizationServiceMock;
    private readonly ComicSourceSettingsDialogViewModel _sut;

    public ComicSourceSettingsDialogViewModelTests()
    {
        _sourceManagerMock = new Mock<ISourceManager>();
        _notificationServiceMock = new Mock<INotificationService>();
        _localizationServiceMock = new Mock<ILocalizationService>();

        _localizationServiceMock
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns<string>(k => k);
        _localizationServiceMock
            .Setup(l => l.GetFormattedString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns<string, object[]>((k, args) => $"{k}:{string.Join(",", args)}");

        _sut = new ComicSourceSettingsDialogViewModel(
            _sourceManagerMock.Object,
            _notificationServiceMock.Object,
            _localizationServiceMock.Object,
            NullLogger<ComicSourceSettingsDialogViewModel>.Instance
        );
    }

    [Fact]
    public async Task InitializeEditAndSave_SerializesAllSettingTypesIntoSavePayload()
    {
        // Arrange
        const string sourceName = "MangaDex";
        var schema = new List<SourceSetting>
        {
            new ToggleSetting
            {
                Key = "data_saver",
                Header = "Data Saver",
                DefaultValue = false,
            },
            new TextSetting
            {
                Key = "mirror",
                Header = "Mirror URL",
                DefaultValue = "https://api.mangadex.org",
            },
            new PasswordSetting { Key = "api_token", Header = "API Token" },
            new NumberSetting
            {
                Key = "concurrency",
                Header = "Concurrency",
                DefaultValue = 4,
                Minimum = 1,
                Maximum = 10,
            },
            new SingleChoiceSetting
            {
                Key = "quality",
                Header = "Quality",
                Options = [new SettingOption("high", "High"), new SettingOption("low", "Low")],
                DefaultKey = "high",
            },
            new MultipleChoiceSetting
            {
                Key = "ratings",
                Header = "Ratings",
                Options =
                [
                    new SettingOption("safe", "Safe"),
                    new SettingOption("suggestive", "Suggestive"),
                ],
                DefaultKeys = ["safe"],
            },
        };

        var storedValues = new Dictionary<string, string>
        {
            ["data_saver"] = "false",
            ["mirror"] = "https://api.mangadex.org",
            ["api_token"] = "existing-secret",
            ["concurrency"] = "4",
            ["quality"] = "high",
            ["ratings"] = "[\"safe\"]",
        };

        _sourceManagerMock
            .Setup(s => s.GetSourceSettingsAsync(sourceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<(
                    IReadOnlyList<SourceSetting> Schema,
                    IReadOnlyDictionary<string, string> Values
                )>.Success((schema, storedValues))
            );

        IReadOnlyDictionary<string, string>? savedPayload = null;
        _sourceManagerMock
            .Setup(s =>
                s.SaveSettingsAsync(
                    sourceName,
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, IReadOnlyDictionary<string, string>, CancellationToken>(
                (_, v, _) => savedPayload = v
            )
            .ReturnsAsync(Result.Success());

        // Act
        await _sut.InitializeAsync(sourceName);

        _sut.Settings.Should().HaveCount(6);
        _sut.CanInteract.Should().BeTrue();
        _sut.IsEmpty.Should().BeFalse();

        ((ToggleSettingViewModel)_sut.Settings[0]).Value = true;
        ((TextSettingViewModel)_sut.Settings[1]).Value = "https://mirror.example.com";
        ((PasswordSettingViewModel)_sut.Settings[2]).Value = "new-secret-token";
        ((NumberSettingViewModel)_sut.Settings[3]).Value = 8;
        var singleVm = (SingleChoiceSettingViewModel)_sut.Settings[4];
        singleVm.SelectedOption = singleVm.Options.Single(o => o.Key == "low");
        var multiVm = (MultipleChoiceSettingViewModel)_sut.Settings[5];
        multiVm.Options.Single(o => o.Key == "suggestive").IsSelected = true;

        await _sut.SaveCommand.ExecuteAsync(null);

        // Assert
        savedPayload.Should().NotBeNull();
        savedPayload!["data_saver"].Should().Be("true");
        savedPayload["mirror"].Should().Be("https://mirror.example.com");
        savedPayload["api_token"].Should().Be("new-secret-token");
        savedPayload["concurrency"].Should().Be("8");
        savedPayload["quality"].Should().Be("low");
        savedPayload["ratings"].Should().Be("[\"safe\",\"suggestive\"]");

        _notificationServiceMock.Verify(
            n => n.ShowSuccess("SuccessComicSourceSettingsSaved", It.IsAny<string>()),
            Times.Once
        );
    }

    [Fact]
    public async Task RestoreDefaults_ResetsValuesInViewModelOnly_WithoutSaving()
    {
        // Arrange
        const string sourceName = "MangaDex";
        var schema = new List<SourceSetting>
        {
            new ToggleSetting
            {
                Key = "data_saver",
                Header = "Data Saver",
                DefaultValue = false,
            },
            new TextSetting
            {
                Key = "mirror",
                Header = "Mirror URL",
                DefaultValue = "https://default.example.com",
            },
            new NumberSetting
            {
                Key = "concurrency",
                Header = "Concurrency",
                DefaultValue = 3,
            },
        };

        var storedValues = new Dictionary<string, string>
        {
            ["data_saver"] = "true",
            ["mirror"] = "https://custom.example.com",
            ["concurrency"] = "9",
        };

        _sourceManagerMock
            .Setup(s => s.GetSourceSettingsAsync(sourceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<(
                    IReadOnlyList<SourceSetting> Schema,
                    IReadOnlyDictionary<string, string> Values
                )>.Success((schema, storedValues))
            );

        await _sut.InitializeAsync(sourceName);

        // Act
        _sut.RestoreDefaultsCommand.Execute(null);

        // Assert
        ((ToggleSettingViewModel)_sut.Settings[0])
            .Value.Should()
            .BeFalse();
        ((TextSettingViewModel)_sut.Settings[1]).Value.Should().Be("https://default.example.com");
        ((NumberSettingViewModel)_sut.Settings[2]).Value.Should().Be(3);

        _sourceManagerMock.Verify(
            s =>
                s.SaveSettingsAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task PasswordSetting_NeverExposesStoredValue_AndRemainsUntouchedWhenUnedited()
    {
        // Arrange
        const string sourceName = "MangaDex";
        var schema = new List<SourceSetting>
        {
            new PasswordSetting { Key = "api_token", Header = "API Token" },
            new PasswordSetting { Key = "secondary_token", Header = "Secondary Token" },
        };

        var storedValues = new Dictionary<string, string>
        {
            ["api_token"] = "super-secret-value",
            ["secondary_token"] = "another-secret",
        };

        _sourceManagerMock
            .Setup(s => s.GetSourceSettingsAsync(sourceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<(
                    IReadOnlyList<SourceSetting> Schema,
                    IReadOnlyDictionary<string, string> Values
                )>.Success((schema, storedValues))
            );

        IReadOnlyDictionary<string, string>? savedPayload = null;
        _sourceManagerMock
            .Setup(s =>
                s.SaveSettingsAsync(
                    sourceName,
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, IReadOnlyDictionary<string, string>, CancellationToken>(
                (_, v, _) => savedPayload = v
            )
            .ReturnsAsync(Result.Success());

        // Act
        await _sut.InitializeAsync(sourceName);

        var untouchedPasswordVm = (PasswordSettingViewModel)_sut.Settings[0];
        var clearedPasswordVm = (PasswordSettingViewModel)_sut.Settings[1];

        // Assert initial state: never displays stored password
        untouchedPasswordVm.Value.Should().BeEmpty();
        untouchedPasswordVm.HasStoredValue.Should().BeTrue();
        clearedPasswordVm.Value.Should().BeEmpty();
        clearedPasswordVm.HasStoredValue.Should().BeTrue();

        // Clear the second password, leave the first untouched
        clearedPasswordVm.ClearCommand.Execute(null);
        clearedPasswordVm.HasStoredValue.Should().BeFalse();

        await _sut.SaveCommand.ExecuteAsync(null);

        // Assert save payload: untouched key is omitted, cleared key is sent as empty string
        savedPayload.Should().NotBeNull();
        savedPayload!.Should().NotContainKey("api_token");
        savedPayload.Should().ContainKey("secondary_token").WhoseValue.Should().BeEmpty();
    }

    [Fact]
    public async Task InitializeAsync_WithUnknownSettingSubclass_SkipsUnknownWithoutCrashing()
    {
        // Arrange
        const string sourceName = "MangaDex";
        var schema = new List<SourceSetting>
        {
            new CustomUnknownSetting { Key = "future_setting", Header = "Future Setting" },
            new ToggleSetting
            {
                Key = "data_saver",
                Header = "Data Saver",
                DefaultValue = true,
            },
        };

        var storedValues = new Dictionary<string, string>();

        _sourceManagerMock
            .Setup(s => s.GetSourceSettingsAsync(sourceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<(
                    IReadOnlyList<SourceSetting> Schema,
                    IReadOnlyDictionary<string, string> Values
                )>.Success((schema, storedValues))
            );

        // Act
        await _sut.InitializeAsync(sourceName);

        // Assert
        _sut.Settings.Should().ContainSingle();
        _sut.Settings[0].Should().BeOfType<ToggleSettingViewModel>();
        _sut.Settings[0].Key.Should().Be("data_saver");
    }
}
