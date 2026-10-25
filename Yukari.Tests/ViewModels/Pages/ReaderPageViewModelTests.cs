using FluentAssertions;
using Moq;
using Yukari.Enums;
using Yukari.Messages;
using Yukari.Messages.Shortcuts;
using Yukari.Models;
using Yukari.Models.Common;
using Yukari.Models.Data;
using Yukari.Models.DTO;
using Yukari.Models.Settings;
using Yukari.Services.Comics;
using Yukari.Services.Settings;
using Yukari.Services.UI;
using Yukari.Tests.TestUtils;
using Yukari.ViewModels.Pages;

namespace Yukari.Tests.ViewModels.Pages;

public class ReaderPageViewModelTests
{
    private const string ComicId = "c-001";
    private const string SourceName = "TestSource";
    private const string ComicTitle = "Test Comic";
    private readonly ContentKey ComicKey = new(ComicId, SourceName);
    private readonly string ChaptersLang = "en";

    private readonly Mock<IComicService> _mockComicService;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<IDialogService> _mockDialogService;
    private readonly Mock<IImageCacheService> _mockImageCacheService;
    private readonly FakeMessenger _mockMessenger;
    private readonly Mock<ILocalizationService> _localizationServiceMock;

    private readonly ReaderPageViewModel _sut;

    public ReaderPageViewModelTests()
    {
        _mockComicService = new Mock<IComicService>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockDialogService = new Mock<IDialogService>();
        _mockImageCacheService = new Mock<IImageCacheService>();
        _mockMessenger = new FakeMessenger();
        _localizationServiceMock = new Mock<ILocalizationService>();

        _mockSettingsService
            .Setup(s => s.Current)
            .Returns(
                new AppSettings()
                {
                    AutoFullscreen = true,
                    ReadingMode = ReadingMode.Vertical,
                    ScalingMode = ScalingMode.FitWidth,
                }
            );
        _mockComicService
            .Setup(c =>
                c.UpsertChapterUserDataAsync(
                    ComicKey,
                    It.IsAny<ContentKey>(),
                    It.IsAny<ChapterUserData>()
                )
            )
            .ReturnsAsync(Result.Success());
        _mockComicService
            .Setup(c =>
                c.UpsertComicReadingProgressAsync(ComicKey, It.IsAny<ComicReadingProgress>())
            )
            .ReturnsAsync(Result.Success());

        _sut = new ReaderPageViewModel(
            _mockComicService.Object,
            _mockSettingsService.Object,
            _mockNotificationService.Object,
            _mockDialogService.Object,
            _mockImageCacheService.Object,
            _mockMessenger,
            _localizationServiceMock.Object
        );
    }

    // ────────────────────────────────────────────────────────────────
    // CONSTRUCTOR
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ShouldLoadUserSettings()
    {
        // Assert
        _sut.IsFullscreen.Should().BeTrue();
        _sut.ReadingMode.Should().Be(ReadingMode.Vertical);
        _sut.ScalingMode.Should().Be(ScalingMode.FitWidth);
        _mockMessenger.GetSingleSentMessage<SetFullscreenMessage>().IsFullscreen.Should().BeTrue();
    }

    // ────────────────────────────────────────────────────────────────
    // MESSAGE HANDLING
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Receive_FullscreenShortcutMessage_ShouldInvertFullscreen()
    {
        // Arrange
        _mockMessenger.Reset();
        var currentFullscreenValue = _sut.IsFullscreen;

        // Act
        _sut.Receive(new FullscreenShortcutMessage());

        // Assert
        _sut.IsFullscreen.Should().Be(!currentFullscreenValue);
        _mockMessenger
            .GetSingleSentMessage<SetFullscreenMessage>()
            .IsFullscreen.Should()
            .Be(!currentFullscreenValue);
    }

    // ────────────────────────────────────────────────────────────────
    // PUBLIC METHODS
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_ShouldLoadLastReadChapter_AndContinueFromLastPageRead()
    {
        // Arrange
        var selectedChapter = new ChapterAggregate(
            new ChapterModel() { Id = "ch-002", Source = SourceName },
            new ChapterUserData() { LastPageRead = 4 }
        );

        var chapters = new ChapterAggregate[]
        {
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-001", Source = SourceName },
                new ChapterUserData()
            ),
            selectedChapter,
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-003", Source = SourceName },
                new ChapterUserData()
            ),
        };

        // Act
        await ChangeSutAsync(selectedChapter.Chapter.Id, false, chapters);

        // Assert
        _sut.CurrentChapter.Should().Be(selectedChapter.Chapter);
        _sut.CurrentPageIndex.Should().Be(selectedChapter.UserData.LastPageRead - 1);
    }

    [Fact]
    public async Task InitializeAsync_ShouldLoadLastReadChapter_AndFirstPage_WhenChapterIsRead()
    {
        // Arrange
        var selectedChapter = new ChapterAggregate(
            new ChapterModel() { Id = "ch-003", Source = SourceName },
            new ChapterUserData() { LastPageRead = 4, IsRead = true }
        );

        var chapters = new ChapterAggregate[]
        {
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-001", Source = SourceName },
                new ChapterUserData()
            ),
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-002", Source = SourceName },
                new ChapterUserData()
            ),
            selectedChapter,
        };

        // Act
        await ChangeSutAsync(selectedChapter.Chapter.Id, false, chapters);

        // Assert
        _sut.CurrentChapter.Should().Be(selectedChapter.Chapter);
        _sut.CurrentPageIndex.Should().Be(0);
    }

    [Fact]
    public async Task InitializeAsync_ShouldLoadFirstChapter_WhenNavigatingFromContinueButton_AndLastReadChapterIsNull()
    {
        // Arrange
        var chapters = new ChapterAggregate[]
        {
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-001", Source = SourceName },
                new ChapterUserData()
            ),
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-002", Source = SourceName },
                new ChapterUserData()
            ),
        };

        // Act
        await ChangeSutAsync(null, true, chapters);

        // Assert
        _sut.CurrentChapter.Should().Be(chapters[0].Chapter);
    }

    [Fact]
    public async Task InitializeAsync_ShouldLoadNextChapter_AndFirstPage_WhenNavigatingFromContinueButton_AndLastReadChapterIsRead()
    {
        // Arrange
        var lastReadChapter = new ChapterAggregate(
            new ChapterModel() { Id = "ch-001", Source = SourceName },
            new ChapterUserData() { LastPageRead = 4, IsRead = true }
        );

        var chapters = new ChapterAggregate[]
        {
            lastReadChapter,
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-002", Source = SourceName },
                new ChapterUserData()
            ),
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-003", Source = SourceName },
                new ChapterUserData()
            ),
        };

        // Act
        await ChangeSutAsync(lastReadChapter.Chapter.Id, true, chapters);

        // Assert
        _sut.CurrentChapter.Should().Be(chapters[1].Chapter);
        _sut.CurrentPageIndex.Should().Be(0);
    }

    [Fact]
    public async Task InitializeAsync_ShouldLoadFirstChapter_AndFirstPage_WhenNavigatingFromContinueButton_AndLastReadChapterIsTheFinalChapter_AndIsRead()
    {
        // Arrange
        var lastReadChapter = new ChapterAggregate(
            new ChapterModel() { Id = "ch-003", Source = SourceName },
            new ChapterUserData() { LastPageRead = 4, IsRead = true }
        );

        var chapters = new ChapterAggregate[]
        {
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-001", Source = SourceName },
                new ChapterUserData()
            ),
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-002", Source = SourceName },
                new ChapterUserData()
            ),
            lastReadChapter,
        };

        // Act
        await ChangeSutAsync(lastReadChapter.Chapter.Id, true, chapters);

        // Assert
        _sut.CurrentChapter.Should().Be(chapters[0].Chapter);
        _sut.CurrentPageIndex.Should().Be(0);
    }

    [Fact]
    public async Task InitializeAsync_WithFilePath_ShouldLoadStandaloneChapter_AndSetTitle()
    {
        // Arrange
        const string filePath = @"C:\Manga\One-Shot Chapter.cbz";
        var chapter = new ChapterModel
        {
            Id = "One-Shot Chapter.cbz",
            Source = "Local",
            Title = "One-Shot Chapter",
        };
        var pages = Enumerable
            .Range(1, 5)
            .Select(i => new ChapterPageModel
            {
                Number = i,
                ImageUrl = $"cbz://{filePath}|{i}.png",
            })
            .ToArray();

        _mockComicService
            .Setup(c => c.GetStandaloneChapterAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<LocalChapterScanResult?>.Success(new LocalChapterScanResult(chapter, pages))
            );

        // Act
        await _sut.InitializeAsync(filePath);

        // Assert
        _sut.ComicTitle.Should().Be("One-Shot Chapter");
        _sut.ChapterTitle.Should().BeNull();
        _sut.CurrentChapter.Should().Be(chapter);
        _sut.ChapterPages.Should().HaveCount(5);
        _sut.CurrentPageIndex.Should().Be(0);
        _sut.ChapterState.Should().Be(LoadState.Loaded);
        _sut.NextChapterCommand.CanExecute(null).Should().BeFalse();
        _sut.PreviousChapterCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_WithFilePath_WhenNullScanResult_ShouldSetErrorState()
    {
        // Arrange
        const string filePath = @"C:\Manga\Empty.cbz";
        _mockComicService
            .Setup(c => c.GetStandaloneChapterAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LocalChapterScanResult?>.Success(null));

        // Act
        await _sut.InitializeAsync(filePath);

        // Assert
        _sut.ChapterState.Should().Be(LoadState.Error);
        _sut.IsWarning.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_WithFilePath_WhenServiceFails_ShouldSetErrorState()
    {
        // Arrange
        const string filePath = @"C:\Manga\Corrupted.cbz";
        _mockComicService
            .Setup(c => c.GetStandaloneChapterAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LocalChapterScanResult?>.Failure("Bad archive", "Error"));

        // Act
        await _sut.InitializeAsync(filePath);

        // Assert
        _sut.ChapterState.Should().Be(LoadState.Error);
        _sut.ChapterErrorMessage.Should().Be("Bad archive");
        _sut.ChapterErrorTitle.Should().Be("Error");
    }

    [Fact]
    public async Task OnNavigatedFrom_WhenPageChangedWithinDebounceWindow_ShouldSavePendingProgressOnce()
    {
        // Arrange
        var chapters = new ChapterAggregate[]
        {
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-001", Source = SourceName },
                new ChapterUserData()
            ),
        };

        await ChangeSutAsync("ch-001", false, chapters);
        _mockComicService.Invocations.Clear();
        _mockMessenger.Reset();

        _sut.NextPageCommand.Execute(null);

        // Act
        _sut.OnNavigatedFrom();
        await WaitForDebounceAsync();

        // Assert
        _mockComicService.Verify(
            c =>
                c.UpsertChapterUserDataAsync(
                    ComicKey,
                    new("ch-001", SourceName),
                    It.Is<ChapterUserData>(u => u.LastPageRead == 2)
                ),
            Times.Once
        );
        _mockMessenger
            .GetSingleSentMessage<ChapterUserDataUpdatedMessage>()
            .ChapterKey.Should()
            .Be(new ContentKey("ch-001", SourceName));
    }

    [Fact]
    public async Task OnNavigatedFrom_WhenStandaloneChapter_ShouldNotSaveProgress()
    {
        // Arrange
        const string filePath = @"C:\Manga\Standalone.cbz";
        var chapter = new ChapterModel
        {
            Id = "Standalone.cbz",
            Source = "Local",
            Title = "Standalone",
        };
        var pages = new[]
        {
            new ChapterPageModel { Number = 1, ImageUrl = $"cbz://{filePath}|1.png" },
            new ChapterPageModel { Number = 2, ImageUrl = $"cbz://{filePath}|2.png" },
        };

        _mockComicService
            .Setup(c => c.GetStandaloneChapterAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<LocalChapterScanResult?>.Success(new LocalChapterScanResult(chapter, pages))
            );

        await _sut.InitializeAsync(filePath);

        // Act
        _sut.NextPageCommand.Execute(null);
        await WaitForDebounceAsync();

        _sut.OnNavigatedFrom();
        await WaitForDebounceAsync();

        // Assert
        _mockComicService.Verify(
            c =>
                c.UpsertChapterUserDataAsync(
                    It.IsAny<ContentKey>(),
                    It.IsAny<ContentKey>(),
                    It.IsAny<ChapterUserData>()
                ),
            Times.Never
        );
        _mockComicService.Verify(
            c =>
                c.UpsertComicReadingProgressAsync(
                    It.IsAny<ContentKey>(),
                    It.IsAny<ComicReadingProgress>()
                ),
            Times.Never
        );
    }

    // ────────────────────────────────────────────────────────────────
    // COMMANDS CanExecute
    // ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(true, false, "ch-001", true)]
    [InlineData(true, false, "ch-003", false)]
    [InlineData(true, true, "ch-002", false)]
    [InlineData(false, false, "ch-001", false)]
    public async Task NextChapterCommand_CanExecute(
        bool HasChapters,
        bool IsLoading,
        string currentChapterId,
        bool expected
    )
    {
        // Arrange
        var chapters = HasChapters
            ? new ChapterAggregate[]
            {
                new ChapterAggregate(
                    new ChapterModel() { Id = "ch-001", Source = SourceName },
                    new ChapterUserData()
                ),
                new ChapterAggregate(
                    new ChapterModel() { Id = "ch-002", Source = SourceName },
                    new ChapterUserData()
                ),
                new ChapterAggregate(
                    new ChapterModel() { Id = "ch-003", Source = SourceName },
                    new ChapterUserData()
                ),
            }
            : null;

        await ChangeSutAsync(currentChapterId, false, chapters);
        _sut.ChapterState = IsLoading ? LoadState.Loading : LoadState.Loaded;

        // Act && Assert
        _sut.NextChapterCommand.CanExecute(null).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, false, "ch-003", true)]
    [InlineData(true, false, "ch-001", false)]
    [InlineData(true, true, "ch-002", false)]
    [InlineData(false, false, "ch-003", false)]
    public async Task PreviousChapterCommand_CanExecute(
        bool HasChapters,
        bool IsLoading,
        string currentChapterId,
        bool expected
    )
    {
        // Arrange
        var chapters = HasChapters
            ? new ChapterAggregate[]
            {
                new ChapterAggregate(
                    new ChapterModel() { Id = "ch-001", Source = SourceName },
                    new ChapterUserData()
                ),
                new ChapterAggregate(
                    new ChapterModel() { Id = "ch-002", Source = SourceName },
                    new ChapterUserData()
                ),
                new ChapterAggregate(
                    new ChapterModel() { Id = "ch-003", Source = SourceName },
                    new ChapterUserData()
                ),
            }
            : null;

        await ChangeSutAsync(currentChapterId, false, chapters);
        _sut.ChapterState = IsLoading ? LoadState.Loading : LoadState.Loaded;

        // Act && Assert
        _sut.PreviousChapterCommand.CanExecute(null).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, 0, true)]
    [InlineData(true, 9, false)]
    [InlineData(false, 4, false)]
    public async Task NextPageCommand_CanExecute(
        bool HasChapters,
        int currentPageIndex,
        bool expected
    )
    {
        // Arrange
        var chapters = HasChapters
            ? new ChapterAggregate[]
            {
                new ChapterAggregate(
                    new ChapterModel() { Id = "ch-001", Source = SourceName },
                    new ChapterUserData() { LastPageRead = currentPageIndex + 1 }
                ),
            }
            : null;

        await ChangeSutAsync("ch-001", false, chapters);

        // Act && Assert
        _sut.NextPageCommand.CanExecute(null).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, 9, true)]
    [InlineData(true, 0, false)]
    [InlineData(false, 5, false)]
    public async Task PreviousPageCommand_CanExecute(
        bool HasChapters,
        int currentPageIndex,
        bool expected
    )
    {
        // Arrange
        var chapters = HasChapters
            ? new ChapterAggregate[]
            {
                new ChapterAggregate(
                    new ChapterModel() { Id = "ch-001", Source = SourceName },
                    new ChapterUserData() { LastPageRead = currentPageIndex + 1 }
                ),
            }
            : null;

        await ChangeSutAsync("ch-001", false, chapters);

        // Act && Assert
        _sut.PreviousPageCommand.CanExecute(null).Should().Be(expected);
    }

    // ────────────────────────────────────────────────────────────────
    // COMMANDS EXECUTION
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void GoBack_ShouldExitFullscreen_AndNavigateToNavigation()
    {
        // Arrange
        _mockMessenger.Reset();

        // Act
        _sut.GoBackCommand.Execute(null);

        // Assert
        _mockMessenger
            .GetSingleSentMessage<SetFullscreenMessage>()
            .IsFullscreen.Should()
            .BeFalse();
        _mockMessenger
            .GetSingleSentMessage<SwitchAppModeMessage>()
            .appMode.Should()
            .Be(AppMode.Navigation);
    }

    [Fact]
    public async Task SetReadingMode_ShouldUpdateReadingMode_AndSaveSettingsImmediately()
    {
        // Act
        await _sut.SetReadingModeCommand.ExecuteAsync(ReadingMode.LeftToRight.ToString());

        // Assert
        _sut.ReadingMode.Should().Be(ReadingMode.LeftToRight);
        _mockSettingsService.Verify(
            s => s.Set(s => s.ReadingMode, ReadingMode.LeftToRight),
            Times.Once
        );
        _mockSettingsService.Verify(s => s.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task SetScalingMode_ShouldUpdateScalingMode_AndSaveSettingsImmediately()
    {
        // Act
        await _sut.SetScalingModeCommand.ExecuteAsync(ScalingMode.FitScreen.ToString());

        // Assert
        _sut.ScalingMode.Should().Be(ScalingMode.FitScreen);
        _mockSettingsService.Verify(
            s => s.Set(s => s.ScalingMode, ScalingMode.FitScreen),
            Times.Once
        );
        _mockSettingsService.Verify(s => s.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task NextChapter_LoadNextChapter_AndSavePreviousChapterReadingProgress()
    {
        // Arrange
        var nextChapter = new ChapterAggregate(
            new ChapterModel() { Id = "ch-002", Source = SourceName },
            new ChapterUserData()
        );

        var chapters = new ChapterAggregate[]
        {
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-001", Source = SourceName },
                new ChapterUserData()
            ),
            nextChapter,
        };

        await ChangeSutAsync("ch-001", false, chapters);
        _mockComicService.Invocations.Clear();

        // Act
        await _sut.NextChapterCommand.ExecuteAsync(null);

        // Assert
        _sut.CurrentChapter.Should().Be(nextChapter.Chapter);
        _mockComicService.Verify(
            c =>
                c.UpsertChapterUserDataAsync(
                    ComicKey,
                    new("ch-001", SourceName),
                    It.IsAny<ChapterUserData>()
                ),
            Times.Once
        );
        _mockComicService.Verify(
            c =>
                c.UpsertComicReadingProgressAsync(
                    ComicKey,
                    It.Is<ComicReadingProgress>(p => p.LastChapterId == "ch-002")
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task NextChapter_WhenPageChangedWithinDebounceWindow_ShouldSavePendingProgressOnce()
    {
        // Arrange
        var chapters = new ChapterAggregate[]
        {
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-001", Source = SourceName },
                new ChapterUserData()
            ),
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-002", Source = SourceName },
                new ChapterUserData()
            ),
        };

        await ChangeSutAsync("ch-001", false, chapters);
        _mockComicService.Invocations.Clear();

        _sut.NextPageCommand.Execute(null);

        // Act
        await _sut.NextChapterCommand.ExecuteAsync(null);
        await WaitForDebounceAsync();

        // Assert
        _mockComicService.Verify(
            c =>
                c.UpsertChapterUserDataAsync(
                    ComicKey,
                    new("ch-001", SourceName),
                    It.Is<ChapterUserData>(u => u.LastPageRead == 2)
                ),
            Times.Once
        );
        _mockComicService.Verify(
            c =>
                c.UpsertChapterUserDataAsync(
                    ComicKey,
                    new("ch-002", SourceName),
                    It.IsAny<ChapterUserData>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task PreviousChapter_LoadPreviousChapter_AndSaveNextChapterReadingProgress()
    {
        // Arrange
        var previousChapter = new ChapterAggregate(
            new ChapterModel() { Id = "ch-001", Source = SourceName },
            new ChapterUserData()
        );

        var chapters = new ChapterAggregate[]
        {
            previousChapter,
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-002", Source = SourceName },
                new ChapterUserData()
            ),
        };

        await ChangeSutAsync("ch-002", false, chapters);
        _mockComicService.Invocations.Clear();

        // Act
        await _sut.PreviousChapterCommand.ExecuteAsync(null);

        // Assert
        _sut.CurrentChapter.Should().Be(previousChapter.Chapter);
        _mockComicService.Verify(
            c =>
                c.UpsertChapterUserDataAsync(
                    ComicKey,
                    new("ch-002", SourceName),
                    It.IsAny<ChapterUserData>()
                ),
            Times.Once
        );
        _mockComicService.Verify(
            c =>
                c.UpsertComicReadingProgressAsync(
                    ComicKey,
                    It.Is<ComicReadingProgress>(p => p.LastChapterId == "ch-001")
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task NextPage_LoadNextPage()
    {
        // Arrange
        var chapters = new ChapterAggregate[]
        {
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-001", Source = SourceName },
                new ChapterUserData()
            ),
        };

        await ChangeSutAsync("ch-001", false, chapters);
        int currentPage = _sut.CurrentPageIndex;

        // Act
        _sut.NextPageCommand.Execute(null);

        // Assert
        _sut.CurrentPageIndex.Should().Be(currentPage + 1);
    }

    [Fact]
    public async Task PreviousPage_LoadPreviousPage()
    {
        // Arrange
        var chapters = new ChapterAggregate[]
        {
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-001", Source = SourceName },
                new ChapterUserData() { LastPageRead = 4 }
            ),
        };

        await ChangeSutAsync("ch-001", false, chapters);
        int currentPage = _sut.CurrentPageIndex;

        // Act
        _sut.PreviousPageCommand.Execute(null);

        // Assert
        _sut.CurrentPageIndex.Should().Be(currentPage - 1);
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(-1, false)]
    [InlineData(10, false)]
    public async Task JumpToPage_SetsCurrentPageIndex_OnlyWhenWithinBounds(
        int targetIndex,
        bool shouldJump
    )
    {
        // Arrange
        var chapters = new ChapterAggregate[]
        {
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-001", Source = SourceName },
                new ChapterUserData()
            ),
        };
        await ChangeSutAsync("ch-001", false, chapters);
        var originalIndex = _sut.CurrentPageIndex;

        // Act
        _sut.JumpToPageCommand.Execute(targetIndex);

        // Assert
        _sut.CurrentPageIndex.Should().Be(shouldJump ? targetIndex : originalIndex);
    }

    // ToggleFullscreen ToggleButton is TwoWay
    [Fact]
    public void ToggleFullscreen_SendMessage()
    {
        // Arrange
        _mockMessenger.Reset();
        var currentFullscreenValue = _sut.IsFullscreen;

        // Act
        _sut.IsFullscreen = !_sut.IsFullscreen; // Simulate TwoWay binding
        _sut.ToggleFullscreenCommand.Execute(null);

        // Assert
        _sut.IsFullscreen.Should().Be(!currentFullscreenValue);
        _mockMessenger
            .GetSingleSentMessage<SetFullscreenMessage>()
            .IsFullscreen.Should()
            .Be(!currentFullscreenValue);
    }

    [Fact]
    public async Task SaveCurrentPageImageAsync_ShouldSanitizeSuggestedFileName()
    {
        // Arrange
        var chapters = new ChapterAggregate[]
        {
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-001", Source = SourceName },
                new ChapterUserData()
            ),
        };

        await ChangeSutAsync("ch-001", false, chapters);
        _sut.ComicTitle = "Comic: Special / Title?";
        _sut.ChapterTitle = "Chapter: 1 <Final> *Draft*";

        string? capturedSuggestedFileName = null;
        _mockDialogService
            .Setup(d =>
                d.OpenFileSavePicker(It.IsAny<string?>(), It.IsAny<Dictionary<string, string[]>?>())
            )
            .Callback<string?, Dictionary<string, string[]>?>(
                (fileName, _) => capturedSuggestedFileName = fileName
            )
            .ReturnsAsync((string?)null);

        // Act
        await _sut.SaveCurrentPageImageCommand.ExecuteAsync(null);

        // Assert
        capturedSuggestedFileName.Should().NotBeNull();
        capturedSuggestedFileName
            .Should()
            .Be("Comic Special  Title - Chapter 1 Final Draft - Page 1");
        capturedSuggestedFileName!.IndexOfAny(Path.GetInvalidFileNameChars()).Should().Be(-1);
    }

    [Fact]
    public async Task SaveCurrentPageImageAsync_WhenStandaloneChapter_ShouldFormatFileNameWithoutChapterTitle()
    {
        // Arrange
        const string filePath = @"C:\Manga\Standalone: One-Shot?.cbz";
        var chapter = new ChapterModel
        {
            Id = "Standalone: One-Shot?.cbz",
            Source = "Local",
            Title = "Standalone: One-Shot?",
        };
        var pages = new[]
        {
            new ChapterPageModel { Number = 1, ImageUrl = $"cbz://{filePath}|1.png" },
        };

        _mockComicService
            .Setup(c => c.GetStandaloneChapterAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<LocalChapterScanResult?>.Success(new LocalChapterScanResult(chapter, pages))
            );

        await _sut.InitializeAsync(filePath);

        string? capturedSuggestedFileName = null;
        _mockDialogService
            .Setup(d =>
                d.OpenFileSavePicker(It.IsAny<string?>(), It.IsAny<Dictionary<string, string[]>?>())
            )
            .Callback<string?, Dictionary<string, string[]>?>(
                (fileName, _) => capturedSuggestedFileName = fileName
            )
            .ReturnsAsync((string?)null);

        // Act
        await _sut.SaveCurrentPageImageCommand.ExecuteAsync(null);

        // Assert
        capturedSuggestedFileName.Should().Be("Standalone One-Shot - Page 1");
    }

    // ────────────────────────────────────────────────────────────────
    // PROPERTY CHANGE EVENTS
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OnReadingModeChanged_ToWebtoon_SyncsWebtoonPageIndexFromCurrentPageIndex()
    {
        // Arrange
        var chapters = new ChapterAggregate[]
        {
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-001", Source = SourceName },
                new ChapterUserData() { LastPageRead = 4 }
            ),
        };
        await ChangeSutAsync("ch-001", false, chapters);
        _sut.CurrentPageIndex.Should().Be(3);

        // Act
        _sut.ReadingMode = ReadingMode.Webtoon;

        // Assert
        _sut.WebtoonPageIndex.Should().Be(3);
    }

    [Fact]
    public async Task OnReadingModeChanged_FromWebtoon_SyncsCurrentPageIndexFromWebtoonPageIndex()
    {
        // Arrange
        var chapters = new ChapterAggregate[]
        {
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-001", Source = SourceName },
                new ChapterUserData()
            ),
        };
        await ChangeSutAsync("ch-001", false, chapters);
        _sut.ReadingMode = ReadingMode.Webtoon;

        _sut.WebtoonPageIndex = 6;
        _sut.CurrentPageIndex.Should().NotBe(6);

        // Act
        _sut.ReadingMode = ReadingMode.Vertical;

        // Assert
        _sut.CurrentPageIndex.Should().Be(6);
    }

    [Fact]
    public async Task OnCurrentPageIndexChanged_WhenPageChanges_ShouldDebounceAndSavePageProgress()
    {
        // Arrange
        var chapters = new ChapterAggregate[]
        {
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-001", Source = SourceName },
                new ChapterUserData()
            ),
        };

        await ChangeSutAsync("ch-001", false, chapters);
        _mockComicService.Invocations.Clear();

        // Act
        _sut.NextPageCommand.Execute(null);

        // Assert
        _mockComicService.Verify(
            c =>
                c.UpsertChapterUserDataAsync(
                    ComicKey,
                    new("ch-001", SourceName),
                    It.IsAny<ChapterUserData>()
                ),
            Times.Never
        );

        await WaitForDebounceAsync();

        _mockComicService.Verify(
            c =>
                c.UpsertChapterUserDataAsync(
                    ComicKey,
                    new("ch-001", SourceName),
                    It.Is<ChapterUserData>(u => u.LastPageRead == 2 && !u.IsRead)
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task OnCurrentPageIndexChanged_WhenChangingPagesRapidly_ShouldSaveOnlyLastPage()
    {
        // Arrange
        var chapters = new ChapterAggregate[]
        {
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-001", Source = SourceName },
                new ChapterUserData()
            ),
        };

        await ChangeSutAsync("ch-001", false, chapters);
        _mockComicService.Invocations.Clear();

        // Act
        _sut.NextPageCommand.Execute(null);
        _sut.NextPageCommand.Execute(null);
        _sut.NextPageCommand.Execute(null);
        await WaitForDebounceAsync();

        // Assert
        _mockComicService.Verify(
            c =>
                c.UpsertChapterUserDataAsync(
                    ComicKey,
                    new("ch-001", SourceName),
                    It.IsAny<ChapterUserData>()
                ),
            Times.Once
        );
        _mockComicService.Verify(
            c =>
                c.UpsertChapterUserDataAsync(
                    ComicKey,
                    new("ch-001", SourceName),
                    It.Is<ChapterUserData>(u => u.LastPageRead == 4)
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task OnCurrentPageIndexChanged_WhenChapterIsRead_ShouldNotSavePageProgress()
    {
        // Arrange
        var chapters = new ChapterAggregate[]
        {
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-001", Source = SourceName },
                new ChapterUserData() { IsRead = true }
            ),
        };

        await ChangeSutAsync("ch-001", false, chapters);
        _mockComicService.Invocations.Clear();

        // Act
        _sut.NextPageCommand.Execute(null);
        await WaitForDebounceAsync();

        // Assert
        _mockComicService.Verify(
            c =>
                c.UpsertChapterUserDataAsync(
                    It.IsAny<ContentKey>(),
                    It.IsAny<ContentKey>(),
                    It.IsAny<ChapterUserData>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task OnCurrentPageIndexChanged_WhenSyncingPageIndex_ShouldNotSavePageProgress()
    {
        // Arrange
        var chapters = new ChapterAggregate[]
        {
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-001", Source = SourceName },
                new ChapterUserData()
            ),
        };

        await ChangeSutAsync("ch-001", false, chapters);
        _mockComicService.Invocations.Clear();

        // Act
        _sut.IsSyncingPageIndex = true;
        _sut.NextPageCommand.Execute(null);
        await WaitForDebounceAsync();

        // Assert
        _mockComicService.Verify(
            c =>
                c.UpsertChapterUserDataAsync(
                    It.IsAny<ContentKey>(),
                    It.IsAny<ContentKey>(),
                    It.IsAny<ChapterUserData>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task OnWebtoonPageIndexChanged_WhenInWebtoonMode_ShouldDebounceAndSavePageProgress()
    {
        // Arrange
        var chapters = new ChapterAggregate[]
        {
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-001", Source = SourceName },
                new ChapterUserData()
            ),
        };

        await ChangeSutAsync("ch-001", false, chapters);
        _sut.ReadingMode = ReadingMode.Webtoon;
        _mockComicService.Invocations.Clear();

        // Act
        _sut.WebtoonPageIndex = 3;
        await WaitForDebounceAsync();

        // Assert
        _mockComicService.Verify(
            c =>
                c.UpsertChapterUserDataAsync(
                    ComicKey,
                    new("ch-001", SourceName),
                    It.Is<ChapterUserData>(u => u.LastPageRead == 4 && !u.IsRead)
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task OnReadingModeChanged_FromWebtoon_ShouldNotSavePageProgress()
    {
        // Arrange
        var chapters = new ChapterAggregate[]
        {
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-001", Source = SourceName },
                new ChapterUserData()
            ),
        };

        await ChangeSutAsync("ch-001", false, chapters);
        _sut.ReadingMode = ReadingMode.Webtoon;
        _sut.WebtoonPageIndex = 6;
        await WaitForDebounceAsync();
        _mockComicService.Invocations.Clear();

        // Act
        _sut.ReadingMode = ReadingMode.Vertical;
        await WaitForDebounceAsync();

        // Assert
        _sut.CurrentPageIndex.Should().Be(6);
        _mockComicService.Verify(
            c =>
                c.UpsertChapterUserDataAsync(
                    It.IsAny<ContentKey>(),
                    It.IsAny<ContentKey>(),
                    It.IsAny<ChapterUserData>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task OnCurrentPageIndexChanged_WhenReachingLastPage_ShouldMarkChapterAsRead()
    {
        // Arrange
        var chapters = new ChapterAggregate[]
        {
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-001", Source = SourceName },
                new ChapterUserData()
            ),
        };

        await ChangeSutAsync("ch-001", false, chapters);
        _mockComicService.Invocations.Clear();

        // Act
        _sut.JumpToPageCommand.Execute(9);
        await WaitForDebounceAsync();

        // Assert
        _mockComicService.Verify(
            c =>
                c.UpsertChapterUserDataAsync(
                    ComicKey,
                    new("ch-001", SourceName),
                    It.Is<ChapterUserData>(u => u.LastPageRead == 10 && u.IsRead)
                ),
            Times.Once
        );
    }

    // ────────────────────────────────────────────────────────────────
    // OTHER
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveChapterProgressAsync_ShouldNotUpdateChapterUserData_WhenChapterIsRead()
    {
        // Arrange
        var chapters = new ChapterAggregate[]
        {
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-001", Source = SourceName },
                new ChapterUserData() { IsRead = true }
            ),
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-002", Source = SourceName },
                new ChapterUserData()
            ),
        };

        await ChangeSutAsync("ch-001", false, chapters);
        _mockComicService.Invocations.Clear();

        // Act
        await _sut.NextChapterCommand.ExecuteAsync(null);

        // Assert
        _mockComicService.Verify(
            c =>
                c.UpsertChapterUserDataAsync(
                    ComicKey,
                    new("ch-001", SourceName),
                    It.IsAny<ChapterUserData>()
                ),
            Times.Never
        );
        _mockComicService.Verify(
            c =>
                c.UpsertComicReadingProgressAsync(
                    ComicKey,
                    It.Is<ComicReadingProgress>(p => p.LastChapterId == "ch-002")
                ),
            Times.Once
        );
    }

    // ────────────────────────────────────────────────────────────────
    // ERROR HANDLING
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_ShouldLoadFirstChapter_AndShowWarning_WhenLastReadChapterNotFound()
    {
        // Arrange
        var selectedChapter = new ChapterAggregate(
            new ChapterModel() { Id = "ch-002", Source = SourceName },
            new ChapterUserData() { LastPageRead = 4 }
        );

        var chapters = new ChapterAggregate[]
        {
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-001", Source = SourceName },
                new ChapterUserData()
            ),
            new ChapterAggregate(
                new ChapterModel() { Id = "ch-003", Source = SourceName },
                new ChapterUserData()
            ),
        };

        // Act
        await ChangeSutAsync(selectedChapter.Chapter.Id, false, chapters);

        // Assert
        _sut.CurrentChapter.Should().Be(chapters[0].Chapter);
        _mockNotificationService.Verify(
            n => n.ShowWarning(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
    }

    // ────────────────────────────────────────────────────────────────
    // UTILITIES
    // ────────────────────────────────────────────────────────────────

    private static Task WaitForDebounceAsync() =>
        Task.Delay(
            ReaderPageViewModel.PageProgressDebounceMs + 100,
            TestContext.Current.CancellationToken
        );

    private async Task ChangeSutAsync(
        string? selectedChapterId = null,
        bool navigationFromContinueButton = false,
        ChapterAggregate[]? chapters = null
    )
    {
        var chaptersList = chapters?.ToList() ?? new List<ChapterAggregate>();

        _mockComicService
            .Setup(c =>
                c.GetAllChaptersAsync(
                    ComicKey,
                    ChaptersLang,
                    false,
                    false,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<IReadOnlyList<ChapterAggregate>>.Success(chaptersList));

        var selectedChapter =
            selectedChapterId != null
                ? chaptersList.FirstOrDefault(c => c.Chapter.Id == selectedChapterId)
                : chaptersList.FirstOrDefault();

        var selectedChapterKey =
            selectedChapterId != null ? new ContentKey(selectedChapterId, SourceName) : null;

        _mockComicService
            .Setup(c =>
                c.GetChapterPagesAsync(
                    ComicKey,
                    It.IsAny<ContentKey>(),
                    false,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<IReadOnlyList<ChapterPageModel>>.Success(
                    Enumerable
                        .Range(1, 10)
                        .Select(i => new ChapterPageModel() { Number = i, ImageUrl = $"{i}" })
                        .ToList()
                )
            );

        await _sut.InitializeAsync(
            ComicKey,
            ComicTitle,
            selectedChapterKey,
            ChaptersLang,
            navigationFromContinueButton
        );
    }
}
