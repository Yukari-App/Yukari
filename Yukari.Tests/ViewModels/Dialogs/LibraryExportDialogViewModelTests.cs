using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Yukari.Models.Common;
using Yukari.Services.Storage;
using Yukari.Services.UI;
using Yukari.ViewModels.Dialogs;

namespace Yukari.Tests.ViewModels.Dialogs;

public class LibraryExportDialogViewModelTests
{
    private readonly Mock<IExportService> _exportServiceMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<ILocalizationService> _localizationServiceMock;
    private readonly LibraryExportDialogViewModel _sut;

    public LibraryExportDialogViewModelTests()
    {
        _exportServiceMock = new Mock<IExportService>();
        _dialogServiceMock = new Mock<IDialogService>();
        _localizationServiceMock = new Mock<ILocalizationService>();

        _sut = new LibraryExportDialogViewModel(
            _exportServiceMock.Object,
            _dialogServiceMock.Object,
            _localizationServiceMock.Object
        );
    }

    [Fact]
    public void InitialState_HasExpectedDefaults()
    {
        _sut.ExportName.Should().StartWith("YukariBackup_");
        _sut.DestinationPath.Should().BeEmpty();
        _sut.IncludeDownloads.Should().BeTrue();
        _sut.IncludePlugins.Should().BeTrue();
        _sut.IsFormVisible.Should().BeTrue();
        _sut.IsExportAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task OpenDestinationPathPicker_UpdatesDestinationPath_WhenFolderSelected()
    {
        // Arrange
        _dialogServiceMock.Setup(d => d.OpenFolderPickerAsync()).ReturnsAsync(@"C:\Backups");

        // Act
        await _sut.OpenDestinationPathPickerCommand.ExecuteAsync(null);

        // Assert
        _sut.DestinationPath.Should().Be(@"C:\Backups");
        _sut.IsExportAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Export_CallsExportLibraryAsync_AndSetsIsExported_WhenSuccess()
    {
        // Arrange
        _sut.DestinationPath = @"C:\Backups";
        _sut.ExportName = "MyLibrary";
        _sut.IncludeDownloads = false;
        _sut.IncludePlugins = true;

        _exportServiceMock
            .Setup(s =>
                s.ExportLibraryAsync(
                    Path.Combine(@"C:\Backups", "MyLibrary.zip"),
                    false,
                    true,
                    It.IsAny<IProgress<(int, int)>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.Success());

        // Act
        await _sut.ExportCommand.ExecuteAsync(null);

        // Assert
        _sut.IsExported.Should().BeTrue();
        _sut.IsError.Should().BeFalse();
        _sut.IsFormVisible.Should().BeFalse();
        _sut.IsExporting.Should().BeFalse();
    }

    [Fact]
    public async Task Export_SetsErrorState_WhenFailure()
    {
        // Arrange
        _sut.DestinationPath = @"C:\Backups";
        _sut.ExportName = "MyLibrary.zip";

        _exportServiceMock
            .Setup(s =>
                s.ExportLibraryAsync(
                    Path.Combine(@"C:\Backups", "MyLibrary.zip"),
                    true,
                    true,
                    It.IsAny<IProgress<(int, int)>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.Failure("Disk full"));

        // Act
        await _sut.ExportCommand.ExecuteAsync(null);

        // Assert
        _sut.IsError.Should().BeTrue();
        _sut.ErrorMessage.Should().Be("Disk full");
        _sut.IsExported.Should().BeFalse();
        _sut.IsFormVisible.Should().BeFalse();
    }
}
