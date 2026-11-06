using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Yukari.Core.Sources.Browser;
using Yukari.Exceptions;
using Yukari.Models;
using Yukari.Services.Sources;
using Yukari.Services.Storage;

namespace Yukari.Tests.Services.Sources;

public class SourceServiceSettingsTests
{
    private readonly Mock<IDataService> _dataServiceMock;
    private readonly Mock<ISourceSettingsStore> _settingsStoreMock;
    private readonly Mock<IBrowserService> _browserServiceMock;
    private readonly SourceService _sourceService;

    public SourceServiceSettingsTests()
    {
        _dataServiceMock = new Mock<IDataService>();
        _settingsStoreMock = new Mock<ISourceSettingsStore>();
        _browserServiceMock = new Mock<IBrowserService>();

        _sourceService = new SourceService(
            _dataServiceMock.Object,
            _settingsStoreMock.Object,
            NullLogger<SourceService>.Instance,
            new HttpClient(),
            _browserServiceMock.Object
        );
    }

    [Fact]
    public async Task ApplySettingsAsync_WhenSourceNotLoaded_DoesNothing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        const string sourceName = "UnloadedSource";

        // Act
        var act = async () => await _sourceService.ApplySettingsAsync(sourceName, ct);

        // Assert
        await act.Should().NotThrowAsync();
        _settingsStoreMock.Verify(s => s.LoadSettingsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<Core.Models.Settings.SourceSetting>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetSettingsSchemaAsync_WhenSourceDisabled_ThrowsComicSourceDisabledException()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        const string sourceName = "DisabledPlugin";
        _dataServiceMock
            .Setup(d => d.GetComicSourceDetailsAsync(sourceName, ct))
            .ReturnsAsync(new ComicSourceModel
            {
                Name = sourceName,
                IsEnabled = false,
                DllPath = "C:\\dummy.dll",
                Version = "1.0.0",
            });

        // Act
        var act = async () => await _sourceService.GetSettingsSchemaAsync(sourceName, ct);

        // Assert
        await act.Should().ThrowAsync<ComicSourceDisabledException>();
    }
}
