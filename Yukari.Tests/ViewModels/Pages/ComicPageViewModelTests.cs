using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Moq;
using Yukari.Enums;
using Yukari.Helpers.UI;
using Yukari.Messages;
using Yukari.Models;
using Yukari.Models.Common;
using Yukari.Models.Data;
using Yukari.Models.DTO;
using Yukari.Services.Comics;
using Yukari.Services.Storage;
using Yukari.Services.UI;
using Yukari.Tests.TestUtils;
using Yukari.ViewModels.Components;
using Yukari.ViewModels.Pages;

namespace Yukari.Tests.ViewModels.Pages;

public class ComicPageViewModelTests
{
    private const string ComicId = "c-001";
    private const string SourceName = "TestSource";
    private const string ComicTitle = "Test Comic";
    private readonly ContentKey ComicKey = new(ComicId, SourceName);

    private readonly Mock<IComicService> _mockComicService;
    private readonly Mock<IDownloadService> _mockDownloadService;
    private readonly Mock<IDialogService> _mockDialogService;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly FakeMessenger _messenger;
    private readonly Mock<ILocalizationService> _localizationServiceMock;

    private readonly ComicPageViewModel _sut;

    public ComicPageViewModelTests()
    {
        _mockComicService = new Mock<IComicService>();
        _mockDownloadService = new Mock<IDownloadService>();
        _mockDialogService = new Mock<IDialogService>();
        _mockNotificationService = new Mock<INotificationService>();
        _messenger = new FakeMessenger();
        _localizationServiceMock = new Mock<ILocalizationService>();

        _sut = new ComicPageViewModel(
            _mockComicService.Object,
            _mockDownloadService.Object,
            _mockDialogService.Object,
            _mockNotificationService.Object,
            _messenger,
            _localizationServiceMock.Object
        );
    }

    // ────────────────────────────────────────────────────────────────
    // PROPERTIES
    // ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, false)]
    [InlineData(6, false)]
    [InlineData(17, true)]
    [InlineData(19, true)]
    public async Task Tags_DisplayProperties_DependsOnTagsCount(int tagsCount, bool hasHidden)
    {
        // Arrange
        var comic = new ComicModel
        {
            Id = ComicId,
            Source = SourceName,
            Title = ComicTitle,
            Tags = Enumerable.Range(1, tagsCount).Select(i => $"Tag {i}").ToArray(),
        };
        await ChangeSutComic(comic);

        // Act & Assert
        _sut.HasHiddenTags.Should().Be(hasHidden);
    }

    [Fact]
    public async Task IsAllChaptersDownload_IsTrue_WhenAllChaptersIsDownloaded()
    {
        // Arrange
        await ChangeSutComic(
            chapters: new List<ChapterAggregate>()
            {
                new ChapterAggregate(
                    new ChapterModel() { Id = "ch-001", Source = SourceName },
                    new ChapterUserData() { IsDownloaded = true }
                ),
                new ChapterAggregate(
                    new ChapterModel() { Id = "ch-002", Source = SourceName },
                    new ChapterUserData() { IsDownloaded = true }
                ),
            }
        );

        // Act & Assert
        _sut.IsAllChaptersDownloaded.Should().BeTrue();
    }

    [Fact]
    public async Task IsAllChaptersDownload_IsFalse_WhenAnyChapterIsNotDownloaded()
    {
        // Arrange
        await ChangeSutComic(
            chapters: new List<ChapterAggregate>()
            {
                new ChapterAggregate(
                    new ChapterModel() { Id = "ch-001", Source = SourceName },
                    new ChapterUserData() { IsDownloaded = true }
                ),
                new ChapterAggregate(
                    new ChapterModel() { Id = "ch-002", Source = SourceName },
                    new ChapterUserData() { IsDownloaded = false }
                ),
            }
        );

        // Act & Assert
        _sut.IsAllChaptersDownloaded.Should().BeFalse();
    }

    [Fact]
    public async Task IsAllChaptersDownload_IsFalse_WhenChaptersIsEmpty()
    {
        // Arrange
        await ChangeSutComic();

        // Act & Assert
        _sut.IsAllChaptersDownloaded.Should().BeFalse();
    }

    [Fact]
    public async Task IsComicAvailable_IsFalse_WhenComicNotAvailable()
    {
        await ChangeSutComic(
            new ComicModel
            {
                Id = ComicId,
                Source = SourceName,
                Title = ComicTitle,
                IsAvailable = false,
            }
        );
        _sut.IsComicAvailable.Should().BeFalse();
    }

    [Fact]
    public void IsComicAvailable_IsTrue_WhenComicIsNull()
    {
        _sut.Comic = null;
        _sut.IsComicAvailable.Should().BeTrue();
    }

    [Fact]
    public void NoChapters_IsTrue_WhenChaptersNotLoading_AndChaptersIsNull()
    {
        // Arrange
        _sut.IsChaptersLoading = false;
        _sut.Chapters = null;

        // Act & Assert
        _sut.NoChapters.Should().BeTrue();
    }

    [Fact]
    public void NoChapters_IsTrue_WhenChaptersNotLoading_AndChaptersIsEmpty()
    {
        // Arrange
        _sut.IsChaptersLoading = false;
        _sut.Chapters = new();

        // Act & Assert
        _sut.NoChapters.Should().BeTrue();
    }

    [Fact]
    public void IsInterfaceReady_IsTrue_WhenNotFavoriteStatusChanging_AndChaptersNotLoading_AndHasChapters()
    {
        // Arrange
        _sut.IsFavoriteStatusChanging = false;
        _sut.IsChaptersLoading = false;
        _sut.Chapters = new() { CreateChapterItemViewModel() };

        // Act & Assert
        _sut.IsInterfaceReady.Should().BeTrue();
    }

    [Fact]
    public void IsInterfaceReady_IsFalse_WhenFavoriteStatusChanging()
    {
        // Arrange
        _sut.IsFavoriteStatusChanging = true;
        _sut.IsChaptersLoading = false;
        _sut.Chapters = new() { CreateChapterItemViewModel() };

        // Act & Assert
        _sut.IsInterfaceReady.Should().BeFalse();
    }

    [Fact]
    public void IsInterfaceReady_IsFalse_WhenChaptersLoading()
    {
        // Arrange
        _sut.IsFavoriteStatusChanging = false;
        _sut.IsChaptersLoading = true;
        _sut.Chapters = new() { CreateChapterItemViewModel() };

        // Act & Assert
        _sut.IsInterfaceReady.Should().BeFalse();
    }

    [Fact]
    public void IsInterfaceReady_IsFalse_WhenNoChapters()
    {
        // Arrange
        _sut.IsFavoriteStatusChanging = false;
        _sut.IsChaptersLoading = false;
        _sut.Chapters = new();

        // Act & Assert
        _sut.IsInterfaceReady.Should().BeFalse();
    }

    [Fact]
    public async Task IsLanguageSelectionAvailable_IsTrue_WhenNotFavoriteStatusChanging_AndChaptersNotLoading_AndHasMoreThanZeroLanguages()
    {
        // Arrange
        _sut.IsFavoriteStatusChanging = false;
        await ChangeSutComic();
        _sut.IsChaptersLoading = false;

        // Act & Assert
        _sut.IsLanguageSelectionAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task IsLanguageSelectionAvailable_IsFalse_WhenFavouriteStatusChanging()
    {
        // Arrange
        _sut.IsFavoriteStatusChanging = true;
        await ChangeSutComic();
        _sut.IsChaptersLoading = false;

        // Act & Assert
        _sut.IsLanguageSelectionAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task IsLanguageSelectionAvailable_IsFalse_WhenChaptersLoading()
    {
        // Arrange
        _sut.IsFavoriteStatusChanging = false;
        await ChangeSutComic();
        _sut.IsChaptersLoading = true;

        // Act & Assert
        _sut.IsLanguageSelectionAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task IsLanguageSelectionAvailable_IsFalse_WhenNoLanguages()
    {
        // Arrange
        _sut.IsFavoriteStatusChanging = false;
        await ChangeSutComic(
            new()
            {
                Id = ComicId,
                Source = SourceName,
                Title = ComicTitle,
            }
        );
        _sut.IsChaptersLoading = false;

        // Act & Assert
        _sut.IsLanguageSelectionAvailable.Should().BeFalse();
    }

    // ────────────────────────────────────────────────────────────────
    // MESSAGE HANDLING
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Receive_ChapterUserDataUpdatedMessage_ShouldRefreshChapterUserData_AndUpdateTotalPages()
    {
        // Arrange
        var chapterItem = CreateChapterItemViewModel(
            new ChapterModel()
            {
                Id = "ch-001",
                Source = SourceName,
                Pages = 5,
            },
            new ChapterUserData() { IsRead = true, LastPageRead = 5 }
        );
        _sut.Chapters = new() { chapterItem };

        _mockComicService
            .Setup(s =>
                s.GetChapterUserDataAsync(ComicKey, chapterItem.Key, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<ChapterUserData>.Success(new() { IsRead = false, LastPageRead = 3 })
            );

        // Act
        _sut.Receive(new ChapterUserDataUpdatedMessage(chapterItem.Key, 10));
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Assert
        chapterItem.LastPageRead.Should().Be(3);
        chapterItem.IsRead.Should().BeFalse();
        chapterItem.TotalPages.Should().Be(10);
    }

    // ────────────────────────────────────────────────────────────────
    // PUBLIC METHODS
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_SetsLoadedState_WhenSuccess()
    {
        // Arrange & Act
        await ChangeSutComic();

        // Assert
        _sut.IsComicLoaded.Should().BeTrue();
        _sut.Comic.Should().NotBeNull();
    }

    [Fact]
    public async Task InitializeAsync_SetsErrorState_WhenComicNotFound()
    {
        // Arrange
        _mockComicService
            .Setup(s =>
                s.GetComicDetailsAsync(It.IsAny<ContentKey>(), false, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Result<ComicAggregate?>.Success(null));
        _mockComicService
            .Setup(s =>
                s.GetAllChaptersAsync(
                    It.IsAny<ContentKey>(),
                    It.IsAny<string>(),
                    false,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<IReadOnlyList<ChapterAggregate>>.Success(Array.Empty<ChapterAggregate>())
            );

        // Act
        await _sut.InitializeAsync(ComicKey);

        // Assert
        _sut.IsComicError.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_SetsErrorState_WhenServiceFails()
    {
        // Arrange
        _mockComicService
            .Setup(s =>
                s.GetComicDetailsAsync(It.IsAny<ContentKey>(), false, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Result<ComicAggregate?>.Failure("Network error", "Error"));
        _mockComicService
            .Setup(s =>
                s.GetAllChaptersAsync(
                    It.IsAny<ContentKey>(),
                    It.IsAny<string>(),
                    false,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<IReadOnlyList<ChapterAggregate>>.Success(Array.Empty<ChapterAggregate>())
            );

        // Act
        await _sut.InitializeAsync(ComicKey);

        // Assert
        _sut.IsComicError.Should().BeTrue();
    }

    // ────────────────────────────────────────────────────────────────
    // COMMANDS CanExecute
    // ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(true, false, false, true, true)]
    [InlineData(null, false, false, true, false)]
    [InlineData(true, true, false, true, false)]
    [InlineData(true, false, true, true, false)]
    [InlineData(true, false, false, false, false)]
    public async Task ContinueReadingCommand_CanExecute(
        bool? hasComic,
        bool isChangingFavoriteStatus,
        bool isLoadingChapters,
        bool hasChapters,
        bool expected
    )
    {
        // Arrange
        if (hasComic.HasValue)
            await ChangeSutComic();
        else
            _sut.Comic = null;

        _sut.IsFavoriteStatusChanging = isChangingFavoriteStatus;
        _sut.IsChaptersLoading = isLoadingChapters;
        _sut.Chapters = hasChapters ? new() { CreateChapterItemViewModel() } : new();

        // Act & Assert
        _sut.ContinueReadingCommand.CanExecute(null).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(null, false, false)]
    [InlineData(true, true, false)]
    public async Task ToggleFavoriteCommand_CanExecute(
        bool? hasComic,
        bool isLoadingChapters,
        bool expected
    )
    {
        // Arrange
        if (hasComic.HasValue)
            await ChangeSutComic();
        else
            _sut.Comic = null;

        _sut.IsChaptersLoading = isLoadingChapters;

        // Act & Assert
        _sut.ToggleFavoriteCommand.CanExecute(null).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(null, false, false)]
    [InlineData(true, true, false)]
    public async Task UpdateCommand_CanExecute(
        bool? hasComic,
        bool isLoadingChapters,
        bool expected
    )
    {
        // Arrange
        if (hasComic.HasValue)
            await ChangeSutComic();
        else
            _sut.Comic = null;

        _sut.IsChaptersLoading = isLoadingChapters;

        // Act & Assert
        _sut.UpdateCommand.CanExecute(null).Should().Be(expected);
    }

    [Theory]
    [InlineData($"http://example.com/{ComicId}", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public async Task OpenInBrowserCommand_CanExecute(string? comicUrl, bool expected)
    {
        // Arrange
        await ChangeSutComic(
            new ComicModel
            {
                Id = ComicId,
                Source = SourceName,
                Title = ComicTitle,
                ComicUrl = comicUrl,
            }
        );

        // Act & Assert
        _sut.OpenInBrowserCommand.CanExecute(null).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, false, false, true, true)]
    [InlineData(false, false, false, true, false)]
    [InlineData(true, true, false, true, false)]
    [InlineData(true, false, true, true, false)]
    [InlineData(true, false, false, false, false)]
    public async Task ToggleDownloadAllChaptersCommand_CanExecute(
        bool comicIsFavorite,
        bool isChangingFavoriteStatus,
        bool isLoadingChapters,
        bool hasChapters,
        bool expected
    )
    {
        // Arrange
        await ChangeSutComic(null, new() { IsFavorite = comicIsFavorite });
        _sut.IsFavoriteStatusChanging = isChangingFavoriteStatus;
        _sut.IsChaptersLoading = isLoadingChapters;
        _sut.Chapters = hasChapters ? new() { CreateChapterItemViewModel() } : new();

        // Act & Assert
        _sut.ToggleDownloadAllChaptersCommand.CanExecute(null).Should().Be(expected);
    }

    // ────────────────────────────────────────────────────────────────
    // COMMANDS EXECUTION
    // ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("ch-001")]
    [InlineData(null)]
    public async Task ContinueReadingAsync_NavigateToReader_WhenSuccess(string? chapterId)
    {
        // Arrange
        await ChangeSutComic();

        _mockComicService
            .Setup(s =>
                s.GetComicReadingProgressAsync(ComicKey, "en", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<ComicReadingProgress>.Success(
                    new ComicReadingProgress { LanguageCode = "en", LastChapterId = chapterId }
                )
            );

        // Act
        await _sut.ContinueReadingCommand.ExecuteAsync(null);

        // Assert
        var message = _messenger.GetSingleSentMessage<SwitchAppModeMessage>();
        message.appMode.Should().Be(AppMode.Reader);

        if (message.Parameter is not ReaderNavigationArgs parameters)
            throw new Exception("Expected Parameter to be of type ReaderNavigationArgs");

        parameters.ComicKey.Should().Be(ComicKey);
        parameters
            .ChapterKey.Should()
            .Be(chapterId != null ? new ContentKey(chapterId, SourceName) : null);
    }

    [Fact]
    public async Task ContinueReadingAsync_ShowErrorNotification_WhenFailure()
    {
        // Arrange
        await ChangeSutComic();

        _mockComicService
            .Setup(s =>
                s.GetComicReadingProgressAsync(
                    It.IsAny<ContentKey>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<ComicReadingProgress>.Failure("DB error", "Error"));

        // Act
        await _sut.ContinueReadingCommand.ExecuteAsync(null);

        // Assert
        _mockNotificationService.Verify(n => n.ShowError("DB error", "Error"), Times.Once);
    }

    [Fact]
    public async Task ToggleFavorite_AddFavorite_AndReloadChapters_WhenSuccess()
    {
        // Arrange
        await ChangeSutComic(null, new ComicUserData { IsFavorite = false });
        _mockComicService
            .Setup(s => s.UpsertFavoriteComicAsync(It.IsAny<ContentKey>()))
            .ReturnsAsync(Result.Success());
        _mockComicService
            .Setup(s => s.UpsertChaptersAsync(It.IsAny<ContentKey>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success());
        _mockComicService
            .Setup(s => s.GetAllChaptersAsync(ComicKey, "en", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<IReadOnlyList<ChapterAggregate>>.Success([
                    new ChapterAggregate(
                        new ChapterModel { Id = "ch-001", Source = SourceName },
                        new()
                    ),
                ])
            );

        // Act
        _sut.IsFavorite = true; // Simulate ToggleButton click
        await _sut.ToggleFavoriteCommand.ExecuteAsync(null);

        // Assert
        _sut.IsFavorite.Should().BeTrue();
        _sut.Chapters.Should().HaveCount(1);
        _sut.IsFavoriteStatusChanging.Should().BeFalse();
        _mockNotificationService.Verify(
            n => n.ShowError(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ToggleFavorite_RemoveFavorite_AndReloadChapters_WhenSuccess()
    {
        // Arrange
        await ChangeSutComic(null, new ComicUserData { IsFavorite = true });
        _mockComicService
            .Setup(s => s.RemoveFavoriteComicAsync(It.IsAny<ContentKey>()))
            .ReturnsAsync(Result.Success());
        _mockComicService
            .Setup(s => s.GetAllChaptersAsync(ComicKey, "en", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<IReadOnlyList<ChapterAggregate>>.Success([
                    new ChapterAggregate(
                        new ChapterModel { Id = "ch-001", Source = SourceName },
                        new()
                    ),
                ])
            );

        // Act
        _sut.IsFavorite = false; // Simulate ToggleButton click
        await _sut.ToggleFavoriteCommand.ExecuteAsync(null);

        // Assert
        _sut.IsFavorite.Should().BeFalse();
        _sut.Chapters.Should().HaveCount(1);
        _sut.IsFavoriteStatusChanging.Should().BeFalse();
        _mockNotificationService.Verify(
            n => n.ShowError(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ToggleFavorite_RevertIsFavorite_WhenEitherAddOrRemoveFails(bool isFavorite)
    {
        // Arrange
        await ChangeSutComic(null, new ComicUserData { IsFavorite = isFavorite });
        _mockComicService
            .Setup(s => s.UpsertFavoriteComicAsync(It.IsAny<ContentKey>()))
            .ReturnsAsync(Result.Failure("DB error", "Error"));
        _mockComicService
            .Setup(s => s.RemoveFavoriteComicAsync(It.IsAny<ContentKey>()))
            .ReturnsAsync(Result.Failure("DB error", "Error"));

        // Act
        _sut.IsFavorite = !isFavorite; // Simulate ToggleButton click
        await _sut.ToggleFavoriteCommand.ExecuteAsync(null);

        // Assert
        _sut.IsFavorite.Should().Be(isFavorite);
        _sut.IsFavoriteStatusChanging.Should().BeFalse();
        _mockNotificationService.Verify(n => n.ShowError("DB error", "Error"), Times.Once);
    }

    [Fact]
    public async Task UpdateCommand_RefreshesComicChaptersAndShowsNotification_WhenSuccess()
    {
        // Arrange
        await ChangeSutComic(null, new() { IsFavorite = true });

        _mockComicService
            .Setup(s => s.UpsertFavoriteComicAsync(It.IsAny<ContentKey>()))
            .ReturnsAsync(Result.Success());
        _mockComicService
            .Setup(s => s.UpsertChaptersAsync(It.IsAny<ContentKey>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success());

        _mockComicService
            .Setup(s =>
                s.GetComicDetailsAsync(It.IsAny<ContentKey>(), false, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<ComicAggregate?>.Success(
                    new ComicAggregate(
                        new ComicModel
                        {
                            Id = ComicId,
                            Source = SourceName,
                            Title = $"{ComicTitle} Updated",
                            Langs = new[] { new LanguageModel("en", "English") },
                        },
                        new ComicUserData { IsFavorite = true }
                    )
                )
            );
        _mockComicService
            .Setup(s => s.GetAllChaptersAsync(ComicKey, "en", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<IReadOnlyList<ChapterAggregate>>.Success([
                    new ChapterAggregate(
                        new ChapterModel { Id = "ch-001", Source = SourceName },
                        new()
                    ),
                    new ChapterAggregate(
                        new ChapterModel { Id = "ch-002", Source = SourceName },
                        new()
                    ),
                ])
            );

        // Act
        await _sut.UpdateCommand.ExecuteAsync(null);

        // Assert
        _sut.Comic?.Title.Should().Be($"{ComicTitle} Updated");
        _sut.Chapters.Should().HaveCount(2);
        _mockNotificationService.Verify(
            n => n.ShowSuccess(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdateCommand_ShowErrorNotification_WhenFailure()
    {
        // Arrange
        await ChangeSutComic(null, new ComicUserData { IsFavorite = true });
        _mockComicService
            .Setup(s => s.UpsertFavoriteComicAsync(It.IsAny<ContentKey>()))
            .ReturnsAsync(Result.Failure("DB error", "Error"));

        // Act
        await _sut.UpdateCommand.ExecuteAsync(null);

        // Assert
        _mockNotificationService.Verify(n => n.ShowError("DB error", "Error"), Times.Once);
    }

    [Fact]
    public async Task ToggleDownloadAllChapters_EnqueuesDownloads_WhenSomeNotDownloaded()
    {
        // Arrange
        var chapter1 = new ChapterModel { Id = "ch-001", Source = SourceName };
        var chapter2 = new ChapterModel { Id = "ch-002", Source = SourceName };
        var chapter3 = new ChapterModel { Id = "ch-003", Source = SourceName };

        await ChangeSutComic(
            userData: new() { IsFavorite = true },
            chapters: new List<ChapterAggregate>
            {
                new ChapterAggregate(chapter1, new ChapterUserData { IsDownloaded = false }),
                new ChapterAggregate(chapter2, new ChapterUserData { IsDownloaded = false }),
                new ChapterAggregate(chapter3, new ChapterUserData { IsDownloaded = true }),
            }
        );

        // Act
        await _sut.ToggleDownloadAllChaptersCommand.ExecuteAsync(null);

        // Assert
        _mockDownloadService.Verify(
            d =>
                d.EnqueueChapterDownload(
                    ComicKey,
                    new ContentKey(chapter1.Id, SourceName),
                    ComicTitle,
                    chapter1.ToDisplayTitle(),
                    It.IsAny<
                        Func<CancellationToken, Task<Result<IReadOnlyList<ChapterPageModel>>>>
                    >()
                ),
            Times.Once
        );
        _mockDownloadService.Verify(
            d =>
                d.EnqueueChapterDownload(
                    ComicKey,
                    new ContentKey(chapter2.Id, SourceName),
                    ComicTitle,
                    chapter2.ToDisplayTitle(),
                    It.IsAny<
                        Func<CancellationToken, Task<Result<IReadOnlyList<ChapterPageModel>>>>
                    >()
                ),
            Times.Once
        );
        _mockDownloadService.Verify(
            d =>
                d.EnqueueChapterDownload(
                    ComicKey,
                    new ContentKey(chapter3.Id, SourceName),
                    ComicTitle,
                    chapter3.ToDisplayTitle(),
                    It.IsAny<
                        Func<CancellationToken, Task<Result<IReadOnlyList<ChapterPageModel>>>>
                    >()
                ),
            Times.Never
        );
        _mockDownloadService.Verify(
            d => d.DeleteChapterDownloadAsync(It.IsAny<ContentKey>(), It.IsAny<ContentKey>()),
            Times.Never
        );
        _mockComicService.Verify(
            s =>
                s.GetAllChaptersAsync(
                    ComicKey,
                    It.IsAny<string>(),
                    false,
                    It.IsAny<CancellationToken>()
                ),
            Times.AtLeastOnce
        );
    }

    [Fact]
    public async Task ToggleDownloadAllChapters_DeleteDownloads_WhenAllDownloaded()
    {
        // Arrange
        var chapter1 = new ChapterModel { Id = "ch-001", Source = SourceName };
        var chapter2 = new ChapterModel { Id = "ch-002", Source = SourceName };

        await ChangeSutComic(
            userData: new() { IsFavorite = true },
            chapters: new List<ChapterAggregate>
            {
                new ChapterAggregate(chapter1, new ChapterUserData { IsDownloaded = true }),
                new ChapterAggregate(chapter2, new ChapterUserData { IsDownloaded = true }),
            }
        );

        // Act
        await _sut.ToggleDownloadAllChaptersCommand.ExecuteAsync(null);

        // Assert
        _mockDownloadService.Verify(
            d => d.DeleteChapterDownloadAsync(ComicKey, new ContentKey(chapter1.Id, SourceName)),
            Times.Once
        );
        _mockDownloadService.Verify(
            d => d.DeleteChapterDownloadAsync(ComicKey, new ContentKey(chapter2.Id, SourceName)),
            Times.Once
        );
        _mockDownloadService.Verify(
            d =>
                d.EnqueueChapterDownload(
                    ComicKey,
                    It.IsAny<ContentKey>(),
                    ComicTitle,
                    It.IsAny<string>(),
                    It.IsAny<
                        Func<CancellationToken, Task<Result<IReadOnlyList<ChapterPageModel>>>>
                    >()
                ),
            Times.Never
        );
        _mockComicService.Verify(
            s =>
                s.GetAllChaptersAsync(
                    ComicKey,
                    It.IsAny<string>(),
                    false,
                    It.IsAny<CancellationToken>()
                ),
            Times.AtLeastOnce
        );
    }

    [Fact]
    public async Task MarkPreviousChaptersAsRead_WhenSuccess()
    {
        // Arrange
        await ChangeSutComic(
            chapters: new List<ChapterAggregate>
            {
                new ChapterAggregate(
                    new ChapterModel
                    {
                        Id = "ch-001",
                        Source = SourceName,
                        Language = "en",
                    },
                    new ChapterUserData { IsRead = false }
                ),
                new ChapterAggregate(
                    new ChapterModel
                    {
                        Id = "ch-002",
                        Source = SourceName,
                        Language = "en",
                    },
                    new ChapterUserData { IsRead = false }
                ),
            }
        );

        _mockComicService
            .Setup(s => s.UpsertChaptersIsReadAsync(ComicKey, new string[] { "ch-001" }, true))
            .ReturnsAsync(Result.Success());
        _mockComicService
            .Setup(s => s.GetAllChaptersAsync(ComicKey, "en", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<IReadOnlyList<ChapterAggregate>>.Success([
                    new ChapterAggregate(
                        new ChapterModel
                        {
                            Id = "ch-001",
                            Source = SourceName,
                            Language = "en",
                        },
                        new ChapterUserData { IsRead = true }
                    ),
                    new ChapterAggregate(
                        new ChapterModel
                        {
                            Id = "ch-002",
                            Source = SourceName,
                            Language = "en",
                        },
                        new ChapterUserData { IsRead = false }
                    ),
                ])
            );

        // Act
        await _sut.MarkPreviousChaptersAsReadCommand.ExecuteAsync(_sut.Chapters[1]);

        // Assert
        _mockComicService.Verify(
            c => c.UpsertChaptersIsReadAsync(ComicKey, new string[] { "ch-001" }, true),
            Times.Once
        );
        _sut.Chapters[0].IsRead.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MarkAllChaptersReadStatus_WhenSuccess(bool isRead)
    {
        // Arrange
        await ChangeSutComic(
            chapters: new List<ChapterAggregate>
            {
                new ChapterAggregate(
                    new ChapterModel
                    {
                        Id = "ch-001",
                        Source = SourceName,
                        Language = "en",
                    },
                    new ChapterUserData { IsRead = !isRead }
                ),
                new ChapterAggregate(
                    new ChapterModel
                    {
                        Id = "ch-002",
                        Source = SourceName,
                        Language = "en",
                    },
                    new ChapterUserData { IsRead = !isRead }
                ),
            }
        );

        _mockComicService
            .Setup(s =>
                s.UpsertChaptersIsReadAsync(ComicKey, new string[] { "ch-001", "ch-002" }, isRead)
            )
            .ReturnsAsync(Result.Success());
        _mockComicService
            .Setup(s => s.GetAllChaptersAsync(ComicKey, "en", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<IReadOnlyList<ChapterAggregate>>.Success([
                    new ChapterAggregate(
                        new ChapterModel
                        {
                            Id = "ch-001",
                            Source = SourceName,
                            Language = "en",
                        },
                        new ChapterUserData { IsRead = isRead }
                    ),
                    new ChapterAggregate(
                        new ChapterModel
                        {
                            Id = "ch-002",
                            Source = SourceName,
                            Language = "en",
                        },
                        new ChapterUserData { IsRead = isRead }
                    ),
                ])
            );

        // Act
        if (isRead)
            await _sut.MarkAllAsReadCommand.ExecuteAsync(null);
        else
            await _sut.MarkAllAsUnreadCommand.ExecuteAsync(null);

        // Assert
        _mockComicService.Verify(
            c => c.UpsertChaptersIsReadAsync(ComicKey, new string[] { "ch-001", "ch-002" }, isRead),
            Times.Once
        );
        _sut.Chapters.Should().OnlyContain(c => c.IsRead == isRead);
    }

    [Fact]
    public async Task MarkPreviousChaptersAsReadStatus_ShowErrorNotification_WhenFailure()
    {
        // Arrange
        await ChangeSutComic();
        _sut.Chapters = new() { CreateChapterItemViewModel(), CreateChapterItemViewModel() };

        _mockComicService
            .Setup(s =>
                s.UpsertChaptersIsReadAsync(It.IsAny<ContentKey>(), It.IsAny<string[]>(), true)
            )
            .ReturnsAsync(Result.Failure("Db Error", "Error"));

        // Act
        await _sut.MarkPreviousChaptersAsReadCommand.ExecuteAsync(_sut.Chapters[1]);

        // Assert
        _mockNotificationService.Verify(n => n.ShowError("Db Error", "Error"), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MarkAllChaptersReadStatus_ShowErrorNotification_WhenFailure(bool isRead)
    {
        // Arrange
        await ChangeSutComic();
        _sut.Chapters = new() { CreateChapterItemViewModel() };

        _mockComicService
            .Setup(s =>
                s.UpsertChaptersIsReadAsync(It.IsAny<ContentKey>(), It.IsAny<string[]>(), isRead)
            )
            .ReturnsAsync(Result.Failure("Db Error", "Error"));

        // Act
        if (isRead)
            await _sut.MarkAllAsReadCommand.ExecuteAsync(null);
        else
            await _sut.MarkAllAsUnreadCommand.ExecuteAsync(null);

        // Assert
        _mockNotificationService.Verify(n => n.ShowError("Db Error", "Error"), Times.Once);
    }

    // ────────────────────────────────────────────────────────────────
    // PROPERTY CHANGE EVENTS
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OnSelectedLangChanged_RefreshChapters_AndUpdateLastSelectedLanguage()
    {
        // Arrange
        await ChangeSutComic();

        _mockComicService
            .Setup(s =>
                s.GetAllChaptersAsync(ComicKey, "pt-BR", false, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<IReadOnlyList<ChapterAggregate>>.Success([
                    new ChapterAggregate(
                        new ChapterModel
                        {
                            Id = "ch-001",
                            Source = SourceName,
                            Language = "pt-BR",
                        },
                        new()
                    ),
                ])
            );

        _mockComicService
            .Setup(s => s.UpsertComicUserDataAsync(ComicKey, It.IsAny<ComicUserData>()))
            .ReturnsAsync(Result.Success());

        // Act
        _sut.SelectedLang = "pt-BR";
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Assert
        _sut.Chapters.Should().HaveCount(1);
        _sut.Chapters.Should().OnlyContain(c => c.Chapter.Language == "pt-BR");
        _mockComicService.Verify(
            s =>
                s.UpsertComicUserDataAsync(
                    ComicKey,
                    It.Is<ComicUserData>(ud => ud.LastSelectedLang == "pt-BR")
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task OnSelectedLangChanged_ShowErrorNotification_WhenFailToSaveUserData()
    {
        // Arrange
        await ChangeSutComic();

        _mockComicService
            .Setup(s =>
                s.GetAllChaptersAsync(ComicKey, "pt-BR", false, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<IReadOnlyList<ChapterAggregate>>.Success(Array.Empty<ChapterAggregate>())
            );

        _mockComicService
            .Setup(s => s.UpsertComicUserDataAsync(ComicKey, It.IsAny<ComicUserData>()))
            .ReturnsAsync(Result.Failure("Db Error", "Error"));

        // Act
        _sut.SelectedLang = "pt-BR";
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Assert
        _mockNotificationService.Verify(s => s.ShowError("Db Error", "Error"), Times.Once);
    }

    // ────────────────────────────────────────────────────────────────
    // UTILITIES
    // ────────────────────────────────────────────────────────────────

    private async Task ChangeSutComic(
        ComicModel? comic = null,
        ComicUserData? userData = null,
        List<ChapterAggregate>? chapters = null
    )
    {
        var comicModel =
            comic
            ?? new ComicModel
            {
                Id = ComicId,
                Source = SourceName,
                Title = ComicTitle,
                Langs = new[] { new LanguageModel("en", "English") },
            };

        _mockComicService
            .Setup(s =>
                s.GetComicDetailsAsync(It.IsAny<ContentKey>(), false, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<ComicAggregate?>.Success(new ComicAggregate(comicModel, userData ?? new()))
            );

        _mockComicService
            .Setup(s =>
                s.GetAllChaptersAsync(
                    It.IsAny<ContentKey>(),
                    It.IsAny<string>(),
                    false,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<IReadOnlyList<ChapterAggregate>>.Success(chapters ?? []));

        await _sut.InitializeAsync(new ContentKey(comicModel.Id, comicModel.Source));
    }

    private ChapterItemViewModel CreateChapterItemViewModel(
        ChapterModel? chapter = null,
        ChapterUserData? userData = null
    ) =>
        new ChapterItemViewModel(
            _mockComicService.Object,
            _mockDownloadService.Object,
            _mockNotificationService.Object,
            new ChapterAggregate(
                chapter ?? new() { Id = Guid.NewGuid().ToString(), Source = SourceName },
                userData ?? new()
            ),
            ComicKey,
            false,
            ComicTitle,
            new RelayCommand<ContentKey>(_ => { }),
            new RelayCommand<ChapterItemViewModel>(_ => { })
        );
}
