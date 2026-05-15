using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Moq;
using Yukari.Enums;
using Yukari.Messages;
using Yukari.Models;
using Yukari.Models.Common;
using Yukari.Models.Data;
using Yukari.Models.DTO;
using Yukari.Services.Comics;
using Yukari.Services.UI;
using Yukari.Tests.TestUtils;
using Yukari.ViewModels.Components;
using Yukari.ViewModels.Pages;

namespace Yukari.Tests.ViewModels.Pages
{
    public class ComicPageViewModelTests
    {
        private readonly Mock<IComicService> _mockComicService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly FakeMessenger _messenger;

        private readonly ComicPageViewModel _sut;

        public ComicPageViewModelTests()
        {
            _mockComicService = new Mock<IComicService>();
            _mockNotificationService = new Mock<INotificationService>();
            _messenger = new FakeMessenger();

            _sut = new ComicPageViewModel(
                _mockComicService.Object,
                _mockNotificationService.Object,
                _messenger
            );
        }

        // ────────────────────────────────────────────────────────────────
        // PROPERTIES
        // ────────────────────────────────────────────────────────────────

        [Theory]
        [InlineData(0, false, "")]
        [InlineData(6, false, "")]
        [InlineData(17, true, "1 are hidden")]
        [InlineData(19, true, "3 are hidden")]
        public async Task Tags_DisplayProperties_DependsOnTagsCount(
            int tagsCount,
            bool hasHidden,
            string extraText
        )
        {
            // Arrange
            var comic = new ComicModel
            {
                Id = "comic-001",
                Source = "TestSource",
                Title = "Test Comic",
                Tags = Enumerable.Range(1, tagsCount).Select(i => $"Tag {i}").ToArray(),
            };
            await ChangeSutComic(comic);

            // Act & Assert
            _sut.HasHiddenTags.Should().Be(hasHidden);
            _sut.HiddenTagsText.Should().Be(extraText);
        }

        // TO-DO: Test IsAllChaptersDownload when implemented

        [Fact]
        public async Task IsComicAvailable_IsFalse_WhenComicNotAvailable()
        {
            await ChangeSutComic(
                new ComicModel
                {
                    Id = "comic-001",
                    Source = "TestSource",
                    Title = "Test Comic",
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
            await ChangeSutComic(
                new ComicModel
                {
                    Id = "comic-001",
                    Source = "TestSource",
                    Title = "Test Comic",
                    Langs = new[] { new LanguageModel("en", "English") },
                }
            );
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
                    Id = "comic-001",
                    Source = "TestSource",
                    Title = "Test Comic",
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
        public async Task Receive_ChapterUserDataUpdatedMessage_ShouldRefreshChapterUserData()
        {
            // Arrange
            var chapterItem = CreateChapterItemViewModel(
                new ChapterModel()
                {
                    Id = "ch-001",
                    Source = "TestSource",
                    Pages = 5,
                }
            );
            _sut.Chapters = new() { chapterItem };

            _mockComicService
                .Setup(s =>
                    s.GetChapterUserDataAsync(
                        new ContentKey("c-001", "TestSource"),
                        chapterItem.Key,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    Result<ChapterUserData>.Success(new() { IsRead = true, LastPageRead = 5 })
                );

            // Act
            _sut.Receive(new ChapterUserDataUpdatedMessage(chapterItem.Key));
            await Task.Delay(100, TestContext.Current.CancellationToken);

            // Assert
            chapterItem.LastPageRead.Should().Be(5);
            chapterItem.IsRead.Should().BeTrue();
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
                    s.GetComicDetailsAsync(
                        It.IsAny<ContentKey>(),
                        false,
                        It.IsAny<CancellationToken>()
                    )
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
            await _sut.InitializeAsync(new ContentKey("c-001", "TestSource"));

            // Assert
            _sut.IsComicError.Should().BeTrue();
        }

        [Fact]
        public async Task InitializeAsync_SetsErrorState_WhenServiceFails()
        {
            // Arrange
            _mockComicService
                .Setup(s =>
                    s.GetComicDetailsAsync(
                        It.IsAny<ContentKey>(),
                        false,
                        It.IsAny<CancellationToken>()
                    )
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
            await _sut.InitializeAsync(new ContentKey("comic-001", "TestSource"));

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
        [InlineData("http://example.com/comic-001", true)]
        [InlineData(null, false)]
        [InlineData("", false)]
        public async Task OpenInBrowserCommand_CanExecute(string? comicUrl, bool expected)
        {
            // Arrange
            await ChangeSutComic(
                new ComicModel
                {
                    Id = "comic-001",
                    Source = "TestSource",
                    Title = "Test Comic",
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
                    s.GetComicReadingProgressAsync(
                        new ContentKey("c-001", "TestSource"),
                        "en",
                        It.IsAny<CancellationToken>()
                    )
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

            parameters.ComicKey.Should().Be(new ContentKey("c-001", "TestSource"));
            parameters
                .ChapterKey.Should()
                .Be(chapterId != null ? new ContentKey(chapterId, "TestSource") : null);
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
                .Setup(s =>
                    s.GetAllChaptersAsync(
                        new ContentKey("c-001", "TestSource"),
                        "en",
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    Result<IReadOnlyList<ChapterAggregate>>.Success([
                        new ChapterAggregate(
                            new ChapterModel { Id = "ch-001", Source = "TestSource" },
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
                .Setup(s =>
                    s.GetAllChaptersAsync(
                        new ContentKey("c-001", "TestSource"),
                        "en",
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    Result<IReadOnlyList<ChapterAggregate>>.Success([
                        new ChapterAggregate(
                            new ChapterModel { Id = "ch-001", Source = "TestSource" },
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
        public async Task ToggleFavorite_RevertIsFavorite_WhenEitherAddOrRemoveFails(
            bool isFavorite
        )
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
                    s.GetComicDetailsAsync(
                        It.IsAny<ContentKey>(),
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    Result<ComicAggregate?>.Success(
                        new ComicAggregate(
                            new ComicModel
                            {
                                Id = "c-001",
                                Source = "TestSource",
                                Title = "Test Comic Updated",
                                Langs = new[] { new LanguageModel("en", "English") },
                            },
                            new ComicUserData { IsFavorite = true }
                        )
                    )
                );
            _mockComicService
                .Setup(s =>
                    s.GetAllChaptersAsync(
                        new ContentKey("c-001", "TestSource"),
                        "en",
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    Result<IReadOnlyList<ChapterAggregate>>.Success([
                        new ChapterAggregate(
                            new ChapterModel { Id = "ch-001", Source = "TestSource" },
                            new()
                        ),
                        new ChapterAggregate(
                            new ChapterModel { Id = "ch-002", Source = "TestSource" },
                            new()
                        ),
                    ])
                );

            // Act
            await _sut.UpdateCommand.ExecuteAsync(null);

            // Assert
            _sut.Comic?.Title.Should().Be("Test Comic Updated");
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

        // TO-DO: Test ToggleDownloadAllChapters when implemented

        [Fact]
        public async Task MarkPreviousChaptersAsRead_WhenSuccess()
        {
            // Arrange
            await ChangeSutComic();
            _sut.Chapters = new()
            {
                CreateChapterItemViewModel(
                    new ChapterModel
                    {
                        Id = "ch-001",
                        Source = "TestSource",
                        Language = "en",
                    },
                    new ChapterUserData { IsRead = false }
                ),
                CreateChapterItemViewModel(
                    new ChapterModel
                    {
                        Id = "ch-002",
                        Source = "TestSource",
                        Language = "en",
                    },
                    new ChapterUserData { IsRead = false }
                ),
            };

            _mockComicService
                .Setup(s =>
                    s.UpsertChaptersIsReadAsync(
                        new("c-001", "TestSource"),
                        new string[] { "ch-001" },
                        true
                    )
                )
                .ReturnsAsync(Result.Success());
            _mockComicService
                .Setup(s =>
                    s.GetAllChaptersAsync(
                        new("c-001", "TestSource"),
                        "en",
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    Result<IReadOnlyList<ChapterAggregate>>.Success([
                        new ChapterAggregate(
                            new ChapterModel
                            {
                                Id = "ch-001",
                                Source = "TestSource",
                                Language = "en",
                            },
                            new ChapterUserData { IsRead = true }
                        ),
                        new ChapterAggregate(
                            new ChapterModel
                            {
                                Id = "ch-002",
                                Source = "TestSource",
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
                c =>
                    c.UpsertChaptersIsReadAsync(
                        new("c-001", "TestSource"),
                        new string[] { "ch-001" },
                        true
                    ),
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
            await ChangeSutComic();
            _sut.Chapters = new()
            {
                CreateChapterItemViewModel(
                    new ChapterModel
                    {
                        Id = "ch-001",
                        Source = "TestSource",
                        Language = "en",
                    },
                    new ChapterUserData { IsRead = !isRead }
                ),
                CreateChapterItemViewModel(
                    new ChapterModel
                    {
                        Id = "ch-002",
                        Source = "TestSource",
                        Language = "en",
                    },
                    new ChapterUserData { IsRead = !isRead }
                ),
            };

            _mockComicService
                .Setup(s =>
                    s.UpsertChaptersIsReadAsync(
                        new("c-001", "TestSource"),
                        new string[] { "ch-001", "ch-002" },
                        isRead
                    )
                )
                .ReturnsAsync(Result.Success());
            _mockComicService
                .Setup(s =>
                    s.GetAllChaptersAsync(
                        new("c-001", "TestSource"),
                        "en",
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    Result<IReadOnlyList<ChapterAggregate>>.Success([
                        new ChapterAggregate(
                            new ChapterModel
                            {
                                Id = "ch-001",
                                Source = "TestSource",
                                Language = "en",
                            },
                            new ChapterUserData { IsRead = isRead }
                        ),
                        new ChapterAggregate(
                            new ChapterModel
                            {
                                Id = "ch-002",
                                Source = "TestSource",
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
                c =>
                    c.UpsertChaptersIsReadAsync(
                        new("c-001", "TestSource"),
                        new string[] { "ch-001", "ch-002" },
                        isRead
                    ),
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
                    s.UpsertChaptersIsReadAsync(
                        It.IsAny<ContentKey>(),
                        It.IsAny<string[]>(),
                        isRead
                    )
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
                    s.GetAllChaptersAsync(
                        new ContentKey("c-001", "TestSource"),
                        "pt-BR",
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    Result<IReadOnlyList<ChapterAggregate>>.Success([
                        new ChapterAggregate(
                            new ChapterModel
                            {
                                Id = "ch-001",
                                Source = "TestSource",
                                Language = "pt-BR",
                            },
                            new()
                        ),
                    ])
                );

            _mockComicService
                .Setup(s =>
                    s.UpsertComicUserDataAsync(
                        new ContentKey("c-001", "TestSource"),
                        It.IsAny<ComicUserData>()
                    )
                )
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
                        new ContentKey("c-001", "TestSource"),
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
                    s.GetAllChaptersAsync(
                        new ContentKey("c-001", "TestSource"),
                        "pt-BR",
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    Result<IReadOnlyList<ChapterAggregate>>.Success(Array.Empty<ChapterAggregate>())
                );

            _mockComicService
                .Setup(s =>
                    s.UpsertComicUserDataAsync(
                        new ContentKey("c-001", "TestSource"),
                        It.IsAny<ComicUserData>()
                    )
                )
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

        private async Task ChangeSutComic(ComicModel? comic = null, ComicUserData? userData = null)
        {
            var comicModel =
                comic
                ?? new ComicModel
                {
                    Id = "c-001",
                    Source = "TestSource",
                    Title = "Test Comic",
                    Langs = new[] { new LanguageModel("en", "English") },
                };

            _mockComicService
                .Setup(s =>
                    s.GetComicDetailsAsync(
                        It.IsAny<ContentKey>(),
                        false,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    Result<ComicAggregate?>.Success(
                        new ComicAggregate(comicModel, userData ?? new())
                    )
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
                .ReturnsAsync(
                    Result<IReadOnlyList<ChapterAggregate>>.Success(Array.Empty<ChapterAggregate>())
                );

            await _sut.InitializeAsync(new ContentKey(comicModel.Id, comicModel.Source));
        }

        private ChapterItemViewModel CreateChapterItemViewModel(
            ChapterModel? chapter = null,
            ChapterUserData? userData = null
        ) =>
            new ChapterItemViewModel(
                _mockComicService.Object,
                _mockNotificationService.Object,
                new ChapterAggregate(
                    chapter ?? new() { Id = Guid.NewGuid().ToString(), Source = "TestSource" },
                    userData ?? new()
                ),
                new ContentKey("c-001", "TestSource"),
                false,
                new RelayCommand<ContentKey>(_ => { }),
                new RelayCommand<ChapterItemViewModel>(_ => { })
            );
    }
}
