using FluentAssertions;
using Moq;
using Yukari.Core.Models;
using Yukari.Messages;
using Yukari.Models;
using Yukari.Models.Common;
using Yukari.Services.Comics;
using Yukari.Services.UI;
using Yukari.Tests.TestUtils;
using Yukari.ViewModels.Components;
using Yukari.ViewModels.Pages;

namespace Yukari.Tests.ViewModels.Pages
{
    public class DiscoverPageViewModelTests
    {
        private readonly Mock<IComicService> _comicServiceMock;
        private readonly Mock<IDialogService> _dialogServiceMock;
        private readonly Mock<INotificationService> _notificationServiceMock;
        private readonly FakeMessenger _fakeMessenger;

        private readonly DiscoverPageViewModel _sut;

        public DiscoverPageViewModelTests()
        {
            _comicServiceMock = new Mock<IComicService>();
            _dialogServiceMock = new Mock<IDialogService>();
            _notificationServiceMock = new Mock<INotificationService>();
            _fakeMessenger = new FakeMessenger();

            SetupSafeDefaultReturns();

            _sut = new DiscoverPageViewModel(
                _comicServiceMock.Object,
                _dialogServiceMock.Object,
                _notificationServiceMock.Object,
                _fakeMessenger
            );
        }

        private void SetupSafeDefaultReturns()
        {
            _comicServiceMock
                .Setup(s => s.GetComicSourcesAsync())
                .ReturnsAsync(
                    Result<IReadOnlyList<ComicSourceModel>>.Success(new List<ComicSourceModel>())
                );

            _comicServiceMock
                .Setup(s => s.GetSourceFiltersAsync(It.IsAny<string>()))
                .ReturnsAsync(Result<IReadOnlyList<Filter>>.Success(new List<Filter>()));

            _comicServiceMock
                .Setup(s =>
                    s.SearchComicsAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<Dictionary<string, IReadOnlyList<string>>>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(Result<IReadOnlyList<ComicModel>>.Success(new List<ComicModel>()));
        }

        // ────────────────────────────────────────────────────────────────
        // PROPERTIES
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void NoSources_IsTrue_WhenComicSourcesIsNull_AndNotLoading()
        {
            // Arrange
            _sut.ComicSources = null;
            _sut.IsContentLoading = false;

            // Act & Assert
            _sut.NoSources.Should().BeTrue();
        }

        [Fact]
        public void NoSources_IsFalse_WhenLoading()
        {
            // Arrange
            _sut.IsContentLoading = true;

            // Act & Assert
            _sut.NoSources.Should().BeFalse();
        }

        [Fact]
        public void NoResults_IsTrue_WhenHasSources_ButNoResults_AndNotLoading()
        {
            // Arrange
            _sut.ComicSources = new List<ComicSourceModel>
            {
                new ComicSourceModel()
                {
                    Name = "TestSource",
                    Version = "1",
                    DllPath = "Yukari.Plugin.TestSource.dll",
                },
            };
            _sut.SearchedComics = new List<ComicItemViewModel>();
            _sut.IsContentLoading = false;

            // Act & Assert
            _sut.NoResults.Should().BeTrue();
        }

        [Fact]
        public void NoResults_IsFalse_WhileLoading()
        {
            // Arrange
            _sut.IsContentLoading = true;

            // Act & Assert
            _sut.NoResults.Should().BeFalse();
        }

        // ────────────────────────────────────────────────────────────────
        // NAVIGATION
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public async Task OnNavigatedTo_LoadSources_WhenDirty_OrEmpty()
        {
            // Arrange
            _sut.ComicSources = null;

            _sut.OnNavigatedFrom();
            _sut.Receive(new ComicSourcesUpdatedMessage());

            _comicServiceMock
                .Setup(s => s.GetComicSourcesAsync())
                .ReturnsAsync(
                    Result<IReadOnlyList<ComicSourceModel>>.Success(
                        new List<ComicSourceModel>
                        {
                            new ComicSourceModel()
                            {
                                Name = "TestSource",
                                Version = "1",
                                DllPath = "Yukari.Plugin.TestSource.dll",
                            },
                        }
                    )
                );

            // Act
            _sut.OnNavigatedTo();

            await Task.Yield();

            // Assert
            _comicServiceMock.Verify(s => s.GetComicSourcesAsync(), Times.Once());
            _sut.ComicSources.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void OnNavigatedTo_SendCurrentSearchText()
        {
            // Arrange
            _sut.Receive(new SearchChangedMessage("test search"));

            // Act
            _sut.OnNavigatedTo();

            // Assert
            var sentMessage = _fakeMessenger.GetSingleSentMessage<SetSearchTextMessage>();

            sentMessage.Should().NotBeNull();
            sentMessage.SearchText.Should().Be("test search");
        }

        // ────────────────────────────────────────────────────────────────
        // MESSAGE HANDLING
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Receive_SearchChangedMessage_ShouldUpdateSearch_AndTriggerComicsUpdate()
        {
            // Arrange
            var message = new SearchChangedMessage("test search");

            await SetSelectedSourceAndWait("TestSource");

            _comicServiceMock
                .Setup(s =>
                    s.SearchComicsAsync(
                        "TestSource",
                        "test search",
                        It.IsAny<Dictionary<string, IReadOnlyList<string>>>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(Result<IReadOnlyList<ComicModel>>.Success(new List<ComicModel>()));

            // Act
            _sut.Receive(message);

            await Task.Yield();

            // Assert
            _comicServiceMock.Verify(
                s =>
                    s.SearchComicsAsync(
                        It.IsAny<string>(),
                        "test search",
                        It.IsAny<Dictionary<string, IReadOnlyList<string>>>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        }

        // ────────────────────────────────────────────────────────────────
        // PROPERTY CHANGERS
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Changing_SelectedComicSource_ShouldLoadFilters_AndComics_WhenChanged()
        {
            // Arrange
            _comicServiceMock
                .Setup(s => s.GetSourceFiltersAsync("TestSource"))
                .ReturnsAsync(
                    Result<IReadOnlyList<Filter>>.Success(
                        new List<Filter>
                        {
                            new Filter(
                                Key: "test",
                                DisplayName: "Test",
                                Options: new List<FilterOption>(),
                                AllowMultiple: true
                            ),
                        }
                    )
                );
            _comicServiceMock
                .Setup(s =>
                    s.SearchComicsAsync(
                        "TestSource",
                        "",
                        It.IsAny<Dictionary<string, IReadOnlyList<string>>>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    Result<IReadOnlyList<ComicModel>>.Success(
                        new List<ComicModel>
                        {
                            new ComicModel()
                            {
                                Id = "123",
                                Source = "TestSource",
                                Title = "TestComic",
                            },
                        }
                    )
                );

            // Act
            await SetSelectedSourceAndWait("TestSource");

            await Task.Yield();

            // Assert
            _comicServiceMock.Verify(s => s.GetSourceFiltersAsync("TestSource"), Times.Once());
            _comicServiceMock.Verify(
                s =>
                    s.SearchComicsAsync(
                        "TestSource",
                        "",
                        It.IsAny<Dictionary<string, IReadOnlyList<string>>>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
            _sut.SearchedComics.Should().HaveCount(1);
            _sut.SearchedComics[0].Comic.Title.Should().Be("TestComic");
            _sut.SearchedComics[0].Comic.Id.Should().Be("123");
            _sut.IsContentLoading.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateAvailableComicSources_ShouldFilterDisabledSources()
        {
            // Arrange
            var Sources = new List<ComicSourceModel>
            {
                new()
                {
                    Name = "EnabledSource",
                    Version = "1",
                    DllPath = "Yukari.Plugin.EnabledPlugin.dll",
                    IsEnabled = true,
                },
                new()
                {
                    Name = "DisabledSource",
                    Version = "1",
                    DllPath = "Yukari.Plugin.DisabledPlugin.dll",
                    IsEnabled = false,
                },
            };

            _comicServiceMock
                .Setup(s => s.GetComicSourcesAsync())
                .ReturnsAsync(Result<IReadOnlyList<ComicSourceModel>>.Success(Sources));

            // Act
            _sut.ComicSources = null;
            _sut.OnNavigatedTo();

            // Assert
            _sut.ComicSources.Should().HaveCount(1);
            _sut.ComicSources.Should().Contain(s => s.Name == "EnabledSource");
        }

        // ────────────────────────────────────────────────────────────────
        // COMMANDS CanExecute
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void FilterCommand_CanExecute_IsFalse_WhileLoading()
        {
            // Arrange
            _sut.IsContentLoading = true;

            // Act & Assert
            _sut.FilterCommand.CanExecute(null).Should().BeFalse();
        }

        [Theory]
        [InlineData(null, 0, false)] // No Source
        [InlineData("TestSource", 0, false)] // No filters
        [InlineData("TestSource1", 1, true)] // 1 filter
        public async Task FilterCommand_CanExecute_AvailabilityDependOnAvailableFilters(
            string? source,
            int filterCount,
            bool expected
        )
        {
            // Arrange
            _comicServiceMock
                .Setup(s => s.GetSourceFiltersAsync(It.IsAny<string>()))
                .ReturnsAsync(
                    Result<IReadOnlyList<Filter>>.Success(
                        Enumerable
                            .Range(0, filterCount)
                            .Select(i => new Filter(
                                Key: $"filter{i}",
                                DisplayName: $"Filter {i}",
                                Options: new List<FilterOption>(),
                                AllowMultiple: true
                            ))
                            .ToList()
                    )
                );

            // Act
            _sut.SelectedComicSource =
                source != null
                    ? new ComicSourceModel
                    {
                        Name = source,
                        Version = "1",
                        DllPath = ".dll",
                    }
                    : null;

            await Task.Yield();
            _sut.IsContentLoading = false;

            // Assert
            _sut.FilterCommand.CanExecute(null).Should().Be(expected);
        }

        // ────────────────────────────────────────────────────────────────
        // COMMANDS EXECUTION
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public async Task OnFilter_ShouldUpdateFilters_WhenDialogReturnsValue()
        {
            // Arrange
            _comicServiceMock
                .Setup(s => s.GetSourceFiltersAsync("TestSource"))
                .ReturnsAsync(
                    Result<IReadOnlyList<Filter>>.Success(
                        new List<Filter>
                        {
                            new Filter(
                                Key: "test",
                                DisplayName: "Test",
                                Options: new List<FilterOption>(),
                                AllowMultiple: true
                            ),
                        }
                    )
                );
            _dialogServiceMock
                .Setup(d =>
                    d.ShowFiltersDialogAsync(
                        It.IsAny<IReadOnlyList<Filter>>(),
                        It.IsAny<Dictionary<string, IReadOnlyList<string>>>()
                    )
                )
                .ReturnsAsync(
                    new Dictionary<string, IReadOnlyList<string>>
                    {
                        ["test"] = new List<string> { "value" },
                    }
                );
            _comicServiceMock
                .Setup(s =>
                    s.SearchComicsAsync(
                        "TestSource",
                        It.IsAny<string>(),
                        It.Is<Dictionary<string, IReadOnlyList<string>>>(f =>
                            f.ContainsKey("test") && f["test"].Contains("value")
                        ),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(Result<IReadOnlyList<ComicModel>>.Success(new List<ComicModel>()));

            // Act
            await SetSelectedSourceAndWait("TestSource");
            await _sut.FilterCommand.ExecuteAsync(null);

            // Assert
            _dialogServiceMock.Verify(
                d =>
                    d.ShowFiltersDialogAsync(
                        It.IsAny<IReadOnlyList<Filter>>(),
                        It.IsAny<Dictionary<string, IReadOnlyList<string>>>()
                    ),
                Times.Once()
            );
            _comicServiceMock.Verify(
                s =>
                    s.SearchComicsAsync(
                        "TestSource",
                        It.IsAny<string>(),
                        It.Is<Dictionary<string, IReadOnlyList<string>>>(f =>
                            f.ContainsKey("test") && f["test"].Contains("value")
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        }

        // ────────────────────────────────────────────────────────────────
        // ERROR HANDLING
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateDisplayedComicsAsync_ShowErrorOnFailure()
        {
            // Arrange
            _comicServiceMock
                .Setup(s => s.GetSourceFiltersAsync("ErrorSource"))
                .ReturnsAsync(Result<IReadOnlyList<Filter>>.Failure("API error"));
            _comicServiceMock
                .Setup(s =>
                    s.SearchComicsAsync(
                        "ErrorSource",
                        It.IsAny<string>(),
                        It.IsAny<Dictionary<string, IReadOnlyList<string>>>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(Result<IReadOnlyList<ComicModel>>.Failure("API error"));

            // Act
            await SetSelectedSourceAndWait("ErrorSource");

            // Assert
            _notificationServiceMock.Verify(n => n.ShowError("API error"), Times.AtLeastOnce);
            _sut.IsContentLoading.Should().BeFalse();
        }

        // ────────────────────────────────────────────────────────────────
        // UTILITIES
        // ────────────────────────────────────────────────────────────────

        private async Task SetSelectedSourceAndWait(string name)
        {
            _sut.SelectedComicSource = new ComicSourceModel
            {
                Name = name,
                Version = "1",
                DllPath = $"Yukari.Plugin.{name}.dll",
            };
            await Task.Yield();
        }
    }
}
