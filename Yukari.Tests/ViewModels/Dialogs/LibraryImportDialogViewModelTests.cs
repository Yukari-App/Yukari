using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Yukari.Enums;
using Yukari.Messages;
using Yukari.Models.Common;
using Yukari.Services.Storage;
using Yukari.Services.UI;
using Yukari.Tests.TestUtils;
using Yukari.ViewModels.Dialogs;

namespace Yukari.Tests.ViewModels.Dialogs;

public class LibraryImportDialogViewModelTests
{
    private readonly Mock<IExportService> _exportServiceMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly FakeMessenger _messenger;
    private readonly Mock<ILocalizationService> _localizationServiceMock;
    private readonly LibraryImportDialogViewModel _sut;

    public LibraryImportDialogViewModelTests()
    {
        _exportServiceMock = new Mock<IExportService>();
        _dialogServiceMock = new Mock<IDialogService>();
        _messenger = new FakeMessenger();
        _localizationServiceMock = new Mock<ILocalizationService>();

        _sut = new LibraryImportDialogViewModel(
            _exportServiceMock.Object,
            _dialogServiceMock.Object,
            _messenger,
            _localizationServiceMock.Object
        );
    }

    [Fact]
    public void InitialState_HasExpectedDefaults()
    {
        _sut.AvailableImportModes.Should().BeEquivalentTo(Enum.GetValues<LibraryImportMode>());
        _sut.ArchivePath.Should().BeEmpty();
        _sut.SelectedImportMode.Should().Be(LibraryImportMode.Merge);
        _sut.IsMergeMode.Should().BeTrue();
        _sut.IsReplaceMode.Should().BeFalse();
        _sut.IsFormVisible.Should().BeTrue();
        _sut.IsImportAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task OpenArchiveFilePicker_UpdatesArchivePath_WhenFileSelected()
    {
        // Arrange
        _dialogServiceMock
            .Setup(d => d.OpenFilePickerAsync(It.IsAny<string[]>()))
            .ReturnsAsync(@"C:\Backups\YukariBackup.zip");

        // Act
        await _sut.OpenArchiveFilePickerCommand.ExecuteAsync(null);

        // Assert
        _sut.ArchivePath.Should().Be(@"C:\Backups\YukariBackup.zip");
        _sut.IsImportAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Import_CallsImportLibraryAsync_AndSendsMessage_WhenSuccess()
    {
        // Arrange
        _sut.ArchivePath = @"C:\Backups\YukariBackup.zip";
        _sut.SelectedImportMode = LibraryImportMode.Replace;

        _exportServiceMock
            .Setup(s =>
                s.ImportLibraryAsync(
                    @"C:\Backups\YukariBackup.zip",
                    LibraryImportMode.Replace,
                    It.IsAny<IProgress<(int, int)>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.Success());

        // Act
        await _sut.ImportCommand.ExecuteAsync(null);

        // Assert
        _sut.IsImported.Should().BeTrue();
        _sut.IsPendingRestart.Should().BeFalse();
        _sut.IsError.Should().BeFalse();
        _sut.IsFormVisible.Should().BeFalse();
        _messenger.SentMessages.Should().ContainItemsAssignableTo<ComicSourcesUpdatedMessage>();
    }

    [Fact]
    public async Task Import_SetsIsPendingRestart_WhenResultIsPendingRestart()
    {
        // Arrange
        _sut.ArchivePath = @"C:\Backups\YukariBackup.zip";

        _exportServiceMock
            .Setup(s =>
                s.ImportLibraryAsync(
                    @"C:\Backups\YukariBackup.zip",
                    LibraryImportMode.Merge,
                    It.IsAny<IProgress<(int, int)>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.PendingRestart());

        // Act
        await _sut.ImportCommand.ExecuteAsync(null);

        // Assert
        _sut.IsImported.Should().BeTrue();
        _sut.IsPendingRestart.Should().BeTrue();
        _sut.IsError.Should().BeFalse();
        _messenger.SentMessages.Should().ContainItemsAssignableTo<ComicSourcesUpdatedMessage>();
    }

    [Fact]
    public async Task Import_SetsErrorState_WhenFailure()
    {
        // Arrange
        _sut.ArchivePath = @"C:\Backups\Corrupt.zip";

        _exportServiceMock
            .Setup(s =>
                s.ImportLibraryAsync(
                    @"C:\Backups\Corrupt.zip",
                    LibraryImportMode.Merge,
                    It.IsAny<IProgress<(int, int)>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.Failure("Invalid backup archive."));

        // Act
        await _sut.ImportCommand.ExecuteAsync(null);

        // Assert
        _sut.IsError.Should().BeTrue();
        _sut.ErrorMessage.Should().Be("Invalid backup archive.");
        _sut.IsImported.Should().BeFalse();
        _sut.IsFormVisible.Should().BeFalse();
    }
}
