using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Moq;
using Yukari.Models;
using Yukari.Models.Common;
using Yukari.Models.Data;
using Yukari.Models.DTO;
using Yukari.Services.Comics;
using Yukari.Services.UI;
using Yukari.ViewModels.Components;

namespace Yukari.Tests.ViewModels.Components;

public class ChapterItemViewModelTests
{
    private readonly Mock<IComicService> _comicServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;

    public ChapterItemViewModelTests()
    {
        _comicServiceMock = new Mock<IComicService>();
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
            Source = "TestSource",
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

    // TO-DO: Test ToggleDownload when implemented

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
            Source = "TestSource",
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
            _notificationServiceMock.Object,
            aggregate,
            new ContentKey("comic-001", "TestSource"),
            isComicFavorite,
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
            _notificationServiceMock.Object,
            aggregate,
            new ContentKey(chapter.Id, chapter.Source),
            isComicFavorite,
            new Mock<IRelayCommand<ContentKey>>().Object,
            new Mock<IRelayCommand<ChapterItemViewModel>>().Object
        );
    }

    private ChapterItemViewModel CreateSutWithUserData(ChapterUserData userData, int? pages = null)
    {
        var chapter = new ChapterModel
        {
            Id = "ch-001",
            Source = "TestSource",
            IsAvailable = true,
            Pages = pages,
            Groups = [],
            LastUpdate = DateOnly.MinValue,
        };
        var aggregate = new ChapterAggregate(chapter, userData);
        return new ChapterItemViewModel(
            _comicServiceMock.Object,
            _notificationServiceMock.Object,
            aggregate,
            new ContentKey("comic-001", "TestSource"),
            isComicFavorite: false,
            new Mock<IRelayCommand<ContentKey>>().Object,
            new Mock<IRelayCommand<ChapterItemViewModel>>().Object
        );
    }
}
