using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Yukari.Core.Models.Settings;
using Yukari.Enums;
using Yukari.Exceptions;
using Yukari.Models;
using Yukari.Models.Common;
using Yukari.Services.Sources;
using Yukari.Services.Storage;
using Yukari.Services.UI;

namespace Yukari.Tests.Services.Sources;

public class SourceManagerSettingsTests
{
    private readonly Mock<IDataService> _dbServiceMock;
    private readonly Mock<ISourceService> _srcServiceMock;
    private readonly Mock<ISourceSettingsStore> _settingsStoreMock;
    private readonly Mock<IDownloadService> _downloadServiceMock;
    private readonly Mock<ILocalizationService> _localizationServiceMock;
    private readonly SourceManager _sourceManager;

    public SourceManagerSettingsTests()
    {
        _dbServiceMock = new Mock<IDataService>();
        _srcServiceMock = new Mock<ISourceService>();
        _settingsStoreMock = new Mock<ISourceSettingsStore>();
        _downloadServiceMock = new Mock<IDownloadService>();
        _localizationServiceMock = new Mock<ILocalizationService>();

        _localizationServiceMock
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns<string>(k => k);
        _localizationServiceMock
            .Setup(l => l.GetFormattedString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns<string, object[]>((k, args) => $"{k}_{string.Join("_", args)}");

        _sourceManager = new SourceManager(
            _dbServiceMock.Object,
            _srcServiceMock.Object,
            _settingsStoreMock.Object,
            _downloadServiceMock.Object,
            _localizationServiceMock.Object,
            NullLogger<SourceManager>.Instance
        );
    }

    [Fact]
    public async Task GetSourceSettingsAsync_ReturnsSchemaAndRawValues()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        const string sourceName = "MangaDex";
        var schema = new List<SourceSetting>
        {
            new ToggleSetting
            {
                Key = "data_saver",
                Header = "Data Saver",
                DefaultValue = false,
            },
        };
        var values = new Dictionary<string, string> { ["data_saver"] = "true" };

        _srcServiceMock.Setup(s => s.GetSettingsSchemaAsync(sourceName, ct)).ReturnsAsync(schema);

        _settingsStoreMock
            .Setup(st => st.LoadRawValuesAsync(sourceName, schema, ct))
            .ReturnsAsync(values);

        // Act
        var result = await _sourceManager.GetSourceSettingsAsync(sourceName, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Schema.Should().BeEquivalentTo(schema);
        result.Value.Values.Should().BeEquivalentTo(values);
    }

    [Fact]
    public async Task GetSourceSettingsAsync_WhenSourceDisabled_ReturnsDisabledResult()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        const string sourceName = "DisabledPlugin";

        _srcServiceMock
            .Setup(s => s.GetSettingsSchemaAsync(sourceName, ct))
            .ThrowsAsync(new ComicSourceDisabledException(sourceName));

        // Act
        var result = await _sourceManager.GetSourceSettingsAsync(sourceName, ct);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.ComicSourceDisabled);
    }

    [Fact]
    public async Task SaveSettingsAsync_PersistsThroughStore_AndCallsApplySettingsOnSourceService()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        const string sourceName = "MangaDex";
        var schema = new List<SourceSetting>
        {
            new ToggleSetting
            {
                Key = "data_saver",
                Header = "Data Saver",
                DefaultValue = false,
            },
        };
        var valuesToSave = new Dictionary<string, string> { ["data_saver"] = "true" };

        _srcServiceMock.Setup(s => s.GetSettingsSchemaAsync(sourceName, ct)).ReturnsAsync(schema);

        _settingsStoreMock
            .Setup(st => st.SaveSettingsAsync(sourceName, schema, valuesToSave, ct))
            .Returns(Task.CompletedTask);

        _srcServiceMock
            .Setup(s => s.ApplySettingsAsync(sourceName, ct))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sourceManager.SaveSettingsAsync(sourceName, valuesToSave, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _settingsStoreMock.Verify(
            st => st.SaveSettingsAsync(sourceName, schema, valuesToSave, ct),
            Times.Once
        );
        _srcServiceMock.Verify(s => s.ApplySettingsAsync(sourceName, ct), Times.Once);
    }
}
