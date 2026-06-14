using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Moq;
using Yukari.Enums;
using Yukari.Models;
using Yukari.Models.Common;
using Yukari.Models.Data;
using Yukari.Models.DTO;
using Yukari.Services.Comics;
using Yukari.Services.Storage;
using Yukari.Services.UI;
using Yukari.ViewModels.Components;

namespace Yukari.Tests.ViewModels.Components;

public class ChapterItemViewModelTests
{
    private const string SourceName = "TestSource";

    private readonly Mock<IComicService> _comicServiceMock;
    private readonly Mock<IDownloadService> _downloadServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;

    public ChapterItemViewModelTests()
    {
        _comicServiceMock = new Mock<IComicService>();
        _downloadServiceMock = new Mock<IDownloadService>();
        _notificationServiceMock = new Mock<INotificationService>();
    }

    // ────────────────────────────────────────────────────────────────
    // PROPERTIES
    // ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    public void IsChapterAvailable_DependsOnAvailabilityAndDownloadStatus(
        bool isAvailable,
        bool isDownloaded,
        bool expected
    )
    {
        // Arrange
        var sut = CreateSut(isAvailable: isAvailable, isDownloaded: isDownloaded);

        // Act & Assert
        sut.IsChapterAvailable.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void IsDownloadAvailable_DependsOnFavoriteAndAvailability(
        bool isFavorite,
        bool isAvailable,
        bool expected
    )
    {
        // Arrange
        var sut = CreateSut(
            isAvailable: isAvailable,
            isDownloaded: false,
            isComicFavorite: isFavorite
        );

        // Act & Assert
        sut.IsDownloadAvailable.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, 10, 10)] // isRead → Pages
    [InlineData(false, 5, 5)] // not read, has lastPageRead → lastPageRead
    [InlineData(false, null, 0)] // not read, no lastPageRead → 0
    public void LastPageRead_Value_DependsOnUserData(bool isRead, int? lastPageRead, int expected)
    {
        // Arrange
        var userData = new ChapterUserData { IsRead = isRead, LastPageRead = lastPageRead };
        var vm = CreateSutWithUserData(userData, pages: 10);

        // Act & Assert
        vm.LastPageRead.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, false, "")]
    [InlineData(3, false, "")]
    [InlineData(4, true, "+1")]
    [InlineData(6, true, "+3")]
    public void Groups_DisplayProperties_DependsOnGroupCount(
        int groupCount,
        bool hasMore,
        string extraText
    )
    {
        // Arrange
        var chapter = new ChapterModel
        {
            Id = "ch-001",
            Source = SourceName,
            Groups = Enumerable.Range(1, groupCount).Select(i => $"Group {i}").ToArray(),
        };
        var sut = CreateSutFromChapter(chapter);

        // Act & Assert
        sut.HasMoreGroups.Should().Be(hasMore);
        sut.ExtraGroupsText.Should().Be(extraText);
    }

    // ────────────────────────────────────────────────────────────────
    // PUBLIC METHODS
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshUserDataAsync_UpdatesData_WhenSuccess()
    {
        // Arrange
        var sut = CreateSut(isDownloaded: false);

        _comicServiceMock
            .Setup(s =>
                s.GetChapterUserDataAsync(
                    It.IsAny<ContentKey>(),
                    It.IsAny<ContentKey>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<ChapterUserData>.Success(
                    new ChapterUserData { IsDownloaded = true, IsRead = true }
                )
            );

        // Act
        await sut.RefreshUserDataAsync();

        // Assert
        sut.IsDownloaded.Should().BeTrue();
        sut.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshUserDataAsync_ShowsError_WhenFailure()
    {
        // Arrange
        var sut = CreateSut();

        _comicServiceMock
            .Setup(s =>
                s.GetChapterUserDataAsync(
                    It.IsAny<ContentKey>(),
                    It.IsAny<ContentKey>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<ChapterUserData>.Failure("Network error"));

        // Act
        await sut.RefreshUserDataAsync();

        // Assert
        _notificationServiceMock.Verify(n => n.ShowError("Network error"), Times.Once);
    }

    // ────────────────────────────────────────────────────────────────
    // COMMANDS EXECUTION
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ToggleDownload_StartsDownload_WhenNotDownloaded()
    {
        // Arrange
        var sut = CreateSut(isComicFavorite: true, isAvailable: true);
        var comicKey = new ContentKey("c-001", SourceName);
        var chapterKey = new ContentKey("ch-001", SourceName);

        _downloadServiceMock
            .Setup(d =>
                d.EnqueueChapterDownload(
                    comicKey,
                    chapterKey,
                    "Test Comic",
                    sut.DisplayTitle!,
                    It.IsAny<
                        Func<CancellationToken, Task<Result<IReadOnlyList<ChapterPageModel>>>>
                    >()
                )
            )
            .Returns(
                new DownloadItem(
                    comicKey,
                    chapterKey,
                    "Test Comic",
                    sut.DisplayTitle!,
                    _ =>
                        Task.FromResult(
                            Result<IReadOnlyList<ChapterPageModel>>.Success(
                                new List<ChapterPageModel>()
                            )
                        )
                )
            );

        // Act
        await sut.ToggleDownloadCommand.ExecuteAsync(null);

        // Assert
        _downloadServiceMock.Verify(
            d =>
                d.EnqueueChapterDownload(
                    comicKey,
                    chapterKey,
                    "Test Comic",
                    sut.DisplayTitle!,
                    It.IsAny<
                        Func<CancellationToken, Task<Result<IReadOnlyList<ChapterPageModel>>>>
                    >()
                ),
            Times.Once
        );
        sut.IsDownloadQueued.Should().BeTrue();
        sut.DownloadButtonValue.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleDownload_CancelDownload_WhenDownloading()
    {
        // Arrange
        var comicKey = new ContentKey("c-001", SourceName);
        var chapterKey = new ContentKey("ch-001", SourceName);

        var downloadItem = new DownloadItem(
            comicKey,
            chapterKey,
            "Test Comic",
            "Test Chapter",
            _ =>
                Task.FromResult(
                    Result<IReadOnlyList<ChapterPageModel>>.Success(new List<ChapterPageModel>())
                )
        );

        _downloadServiceMock.Setup(d => d.GetDownload(comicKey, chapterKey)).Returns(downloadItem);

        var sut = CreateSut(isComicFavorite: true, isAvailable: true);

        // Act
        await sut.ToggleDownloadCommand.ExecuteAsync(null);

        // Assert
        downloadItem.Status.Should().Be(DownloadStatus.Cancelled);
        sut.IsDownloaded.Should().BeFalse();
        sut.DownloadButtonValue.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleDownload_DeleteDownload_WhenDownloaded()
    {
        // Arrange
        var comicKey = new ContentKey("c-001", SourceName);
        var chapterKey = new ContentKey("ch-001", SourceName);

        var sut = CreateSut(isComicFavorite: true, isDownloaded: true, isAvailable: true);

        // Act
        await sut.ToggleDownloadCommand.ExecuteAsync(null);

        // Assert
        _downloadServiceMock.Verify(
            d => d.DeleteChapterDownloadAsync(comicKey, chapterKey),
            Times.Once
        );
        sut.IsDownloaded.Should().BeFalse();
        sut.DownloadButtonValue.Should().BeFalse();
    }

    // Comment about ToggleRead: Since ToggleButton is TwoWay, it changes the IsRead property before the command executes.
    [Fact]
    public async Task ToggleRead_UpdatesLastPageRead_WhenSuccess()
    {
        // Arrange
        _comicServiceMock
            .Setup(s =>
                s.UpsertChapterUserDataAsync(
                    It.IsAny<ContentKey>(),
                    It.IsAny<ContentKey>(),
                    It.IsAny<ChapterUserData>()
                )
            )
            .ReturnsAsync(Result.Success());

        var sut = CreateSutWithUserData(
            new ChapterUserData { IsRead = false, LastPageRead = null },
            pages: 10
        );

        // Act
        sut.IsRead = !sut.IsRead; // Simulate ToggleButton click
        await sut.ToggleReadCommand.ExecuteAsync(null);

        // Assert
        sut.LastPageRead.Should().Be(10); // IsRead=true → Pages
    }

    [Fact]
    public async Task ToggleRead_RevertsIsRead_WhenFailure()
    {
        // Arrange
        _comicServiceMock
            .Setup(s =>
                s.UpsertChapterUserDataAsync(
                    It.IsAny<ContentKey>(),
                    It.IsAny<ContentKey>(),
                    It.IsAny<ChapterUserData>()
                )
            )
            .ReturnsAsync(Result.Failure("Error"));

        var sut = CreateSutWithUserData(new ChapterUserData { IsRead = true }, pages: 10);
        var originalIsRead = sut.IsRead;

        // Act
        sut.IsRead = !sut.IsRead; // Simulate ToggleButton click
        await sut.ToggleReadCommand.ExecuteAsync(null);

        // Assert
        sut.IsRead.Should().Be(originalIsRead);
        _notificationServiceMock.Verify(n => n.ShowError(It.IsAny<string>()), Times.Once);
    }

    // ────────────────────────────────────────────────────────────────
    // DOWNLOAD ITEM PROPERTY CHANGED EVENTS
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DownloadStatusProperty_Update_WhenDownloadItemStatusChanged()
    {
        // Arrange
        var comicKey = new ContentKey("c-001", SourceName);
        var chapterKey = new ContentKey("ch-001", SourceName);

        var downloadItem = new DownloadItem(
            comicKey,
            chapterKey,
            "Test Comic",
            "Test Chapter",
            _ =>
                Task.FromResult(
                    Result<IReadOnlyList<ChapterPageModel>>.Success(new List<ChapterPageModel>())
                )
        );

        _downloadServiceMock.Setup(d => d.GetDownload(comicKey, chapterKey)).Returns(downloadItem);

        _comicServiceMock
            .Setup(c => c.GetChapterUserDataAsync(comicKey, chapterKey))
            .ReturnsAsync(
                Result<ChapterUserData>.Success(new ChapterUserData() { IsDownloaded = true })
            );

        var sut = CreateSut(isComicFavorite: true, isAvailable: true);

        // Act & Assert
        downloadItem.Status = DownloadStatus.Queued;
        sut.IsDownloadQueued.Should().BeTrue();
        sut.IsDownloading.Should().BeFalse();
        sut.IsDownloadCancelled.Should().BeFalse();
        sut.IsDownloadFailed.Should().BeFalse();
        sut.IsDownloaded.Should().BeFalse();

        downloadItem.Status = DownloadStatus.Downloading;
        sut.IsDownloadQueued.Should().BeFalse();
        sut.IsDownloading.Should().BeTrue();
        sut.IsDownloadCancelled.Should().BeFalse();
        sut.IsDownloadFailed.Should().BeFalse();
        sut.IsDownloaded.Should().BeFalse();

        downloadItem.Status = DownloadStatus.Completed;
        sut.IsDownloadQueued.Should().BeFalse();
        sut.IsDownloading.Should().BeFalse();
        sut.IsDownloadCancelled.Should().BeFalse();
        sut.IsDownloadFailed.Should().BeFalse();
        sut.IsDownloaded.Should().BeTrue();
    }

    // ────────────────────────────────────────────────────────────────
    // UTILITIES
    // ────────────────────────────────────────────────────────────────

    private ChapterItemViewModel CreateSut(
        bool isAvailable = true,
        bool isDownloaded = false,
        bool isComicFavorite = false,
        int? pages = null
    )
    {
        var chapter = new ChapterModel
        {
            Id = "ch-001",
            Source = SourceName,
            IsAvailable = isAvailable,
            Pages = pages,
            Groups = [],
            LastUpdate = DateOnly.MinValue,
        };

        var aggregate = new ChapterAggregate(
            chapter,
            new ChapterUserData { IsDownloaded = isDownloaded }
        );

        return new ChapterItemViewModel(
            _comicServiceMock.Object,
            _downloadServiceMock.Object,
            _notificationServiceMock.Object,
            aggregate,
            new ContentKey("c-001", SourceName),
            isComicFavorite,
            "Test Comic",
            new Mock<IRelayCommand<ContentKey>>().Object,
            new Mock<IRelayCommand<ChapterItemViewModel>>().Object
        );
    }

    private ChapterItemViewModel CreateSutFromChapter(
        ChapterModel chapter,
        bool isComicFavorite = false
    )
    {
        var aggregate = new ChapterAggregate(chapter, new ChapterUserData());
        return new ChapterItemViewModel(
            _comicServiceMock.Object,
            _downloadServiceMock.Object,
            _notificationServiceMock.Object,
            aggregate,
            new ContentKey(chapter.Id, chapter.Source),
            isComicFavorite,
            "Test Comic",
            new Mock<IRelayCommand<ContentKey>>().Object,
            new Mock<IRelayCommand<ChapterItemViewModel>>().Object
        );
    }

    private ChapterItemViewModel CreateSutWithUserData(ChapterUserData userData, int? pages = null)
    {
        var chapter = new ChapterModel
        {
            Id = "ch-001",
            Source = SourceName,
            IsAvailable = true,
            Pages = pages,
            Groups = [],
            LastUpdate = DateOnly.MinValue,
        };
        var aggregate = new ChapterAggregate(chapter, userData);
        return new ChapterItemViewModel(
            _comicServiceMock.Object,
            _downloadServiceMock.Object,
            _notificationServiceMock.Object,
            aggregate,
            new ContentKey("c-001", SourceName),
            false,
            "Test Comic",
            new Mock<IRelayCommand<ContentKey>>().Object,
            new Mock<IRelayCommand<ChapterItemViewModel>>().Object
        );
    }
}
