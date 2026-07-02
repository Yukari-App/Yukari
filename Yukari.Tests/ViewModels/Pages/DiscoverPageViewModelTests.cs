using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Yukari.Core.Models;
using Yukari.Messages;
using Yukari.Models;
using Yukari.Models.Common;
using Yukari.Services.Comics;
using Yukari.Services.Settings;
using Yukari.Services.UI;
using Yukari.Tests.TestUtils;
using Yukari.ViewModels.Components;
using Yukari.ViewModels.Pages;

namespace Yukari.Tests.ViewModels.Pages;

public class DiscoverPageViewModelTests
{
    // Default Source Mock
    private readonly ComicSourceModel DefaultMockSource = new()
    {
        Name = "TestSource",
        Version = "1",
        DllPath = "Yukari.Plugin.TestSource.dll",
    };

    private readonly Mock<IComicService> _comicServiceMock;
    private readonly Mock<ISettingsService> _settingsServiceMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly FakeMessenger _fakeMessenger;
    private readonly Mock<ILocalizationService> _localizationServiceMock;

    private readonly DiscoverPageViewModel _sut;

    public DiscoverPageViewModelTests()
    {
        _comicServiceMock = new Mock<IComicService>();
        _settingsServiceMock = new Mock<ISettingsService>();
        _dialogServiceMock = new Mock<IDialogService>();
        _notificationServiceMock = new Mock<INotificationService>();
        _fakeMessenger = new FakeMessenger();
        _localizationServiceMock = new Mock<ILocalizationService>();

        SetupSafeDefaultReturns();

        _sut = new DiscoverPageViewModel(
            _comicServiceMock.Object,
            _settingsServiceMock.Object,
            _dialogServiceMock.Object,
            _notificationServiceMock.Object,
            _fakeMessenger,
            _localizationServiceMock.Object
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
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<IReadOnlyList<ComicModel>>.Success(new List<ComicModel>()));
    }

    // ────────────────────────────────────────────────────────────────
    // PROPERTIES
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void IsLoadMoreVisibile_IsTrue_WhenHasResults_AndHasSources_AndNotLoading()
    {
        // Arrange
        _sut.IsContentLoading = false;
        _sut.ComicSources = new List<ComicSourceModel>() { DefaultMockSource };
        _sut.SearchedComics = new ObservableCollection<ComicItemViewModel>()
        {
            new ComicItemViewModel(
                new ComicModel()
                {
                    Id = "c-001",
                    Source = DefaultMockSource.Name,
                    Title = "Test Comic",
                }
            ),
        };

        // Act & Assert
        _sut.IsLoadMoreVisible.Should().BeTrue();
    }

    [Fact]
    public void IsLoadMoreVisibile_IsFalse_WhenNoResults()
    {
        // Arrange
        _sut.IsContentLoading = false;
        _sut.ComicSources = new List<ComicSourceModel>() { DefaultMockSource };
        _sut.SearchedComics = [];

        // Act & Assert
        _sut.IsLoadMoreVisible.Should().BeFalse();
    }

    [Fact]
    public void IsLoadMoreVisibile_IsFalse_WhenNoSources()
    {
        // Arrange
        _sut.IsContentLoading = false;
        _sut.ComicSources = [];
        _sut.SearchedComics = [];

        // Act & Assert
        _sut.IsLoadMoreVisible.Should().BeFalse();
    }

    [Fact]
    public void IsLoadMoreVisibile_IsFalse_WhenLoading()
    {
        // Arrange
        _sut.IsContentLoading = true;

        // Act & Assert
        _sut.IsLoadMoreVisible.Should().BeFalse();
    }

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
        _sut.ComicSources = new List<ComicSourceModel> { DefaultMockSource };
        _sut.SearchedComics = new ObservableCollection<ComicItemViewModel>();
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
                    new List<ComicSourceModel> { DefaultMockSource }
                )
            );

        // Act
        _sut.OnNavigatedTo();

        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Assert
        _comicServiceMock.Verify(s => s.GetComicSourcesAsync(), Times.Once);
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

        await SetSelectedSourceAndWait(DefaultMockSource.Name);

        _comicServiceMock
            .Setup(s =>
                s.SearchComicsAsync(
                    DefaultMockSource.Name,
                    "test search",
                    It.IsAny<Dictionary<string, IReadOnlyList<string>>>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<IReadOnlyList<ComicModel>>.Success(new List<ComicModel>()));

        // Act
        _sut.Receive(message);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Assert
        _comicServiceMock.Verify(
            s =>
                s.SearchComicsAsync(
                    It.IsAny<string>(),
                    "test search",
                    It.IsAny<Dictionary<string, IReadOnlyList<string>>>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
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
    [InlineData(false, 0, false)] // No Source
    [InlineData(true, 0, false)] // No filters
    [InlineData(true, 1, true)] // 1 filter
    public async Task FilterCommand_CanExecute_AvailabilityDependOnAvailableFilters(
        bool sourceHasValue,
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
        _sut.SelectedComicSource = sourceHasValue ? DefaultMockSource : null;

        await Task.Delay(100, TestContext.Current.CancellationToken);
        _sut.IsContentLoading = false;

        // Assert
        _sut.FilterCommand.CanExecute(null).Should().Be(expected);
    }

    [Theory]
    [InlineData(false, false, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(false, false, false, false)]
    public async Task LoadMoreCommand_CanExecute_AvailabilityDependOnPageState(
        bool contentLoading,
        bool loadingMore,
        bool hasResults,
        bool expected
    )
    {
        // Arrange
        _sut.ComicSources = new List<ComicSourceModel>() { DefaultMockSource };

        _sut.IsContentLoading = contentLoading;
        _sut.IsLoadingMore = loadingMore;
        _sut.SearchedComics = hasResults
            ? new ObservableCollection<ComicItemViewModel>()
            {
                new ComicItemViewModel(
                    new ComicModel()
                    {
                        Id = "c-001",
                        Source = DefaultMockSource.Name,
                        Title = "Test Comic",
                    }
                ),
            }
            : new ObservableCollection<ComicItemViewModel>();

        // Act & Assert
        _sut.LoadMoreCommand.CanExecute(null).Should().Be(expected);
    }

    // ────────────────────────────────────────────────────────────────
    // COMMANDS EXECUTION
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OnFilter_ShouldUpdateFilters_WhenDialogReturnsValue()
    {
        // Arrange
        _comicServiceMock
            .Setup(s => s.GetSourceFiltersAsync(DefaultMockSource.Name))
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
                    DefaultMockSource.Name,
                    It.IsAny<string>(),
                    It.Is<Dictionary<string, IReadOnlyList<string>>>(f =>
                        f.ContainsKey("test") && f["test"].Contains("value")
                    ),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<IReadOnlyList<ComicModel>>.Success(new List<ComicModel>()));

        // Act
        await SetSelectedSourceAndWait(DefaultMockSource.Name);
        await _sut.FilterCommand.ExecuteAsync(null);

        // Assert
        _dialogServiceMock.Verify(
            d =>
                d.ShowFiltersDialogAsync(
                    It.IsAny<IReadOnlyList<Filter>>(),
                    It.IsAny<Dictionary<string, IReadOnlyList<string>>>()
                ),
            Times.Once
        );
        _comicServiceMock.Verify(
            s =>
                s.SearchComicsAsync(
                    DefaultMockSource.Name,
                    It.IsAny<string>(),
                    It.Is<Dictionary<string, IReadOnlyList<string>>>(f =>
                        f.ContainsKey("test") && f["test"].Contains("value")
                    ),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task LoadMore_ShouldIncreaseComics_WhenSearchComicReturnNewComics()
    {
        // Arrange
        _sut.ComicSources = new List<ComicSourceModel>() { DefaultMockSource };
        _sut.SelectedComicSource = DefaultMockSource;

        _sut.SearchedComics = new ObservableCollection<ComicItemViewModel>()
        {
            new ComicItemViewModel(
                new ComicModel()
                {
                    Id = "c-001",
                    Source = DefaultMockSource.Name,
                    Title = "Test Comic",
                }
            ),
        };

        _comicServiceMock
            .Setup(c =>
                c.SearchComicsAsync(
                    DefaultMockSource.Name,
                    It.IsAny<string?>(),
                    It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(),
                    2,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<IReadOnlyList<ComicModel>>.Success(
                    new List<ComicModel>()
                    {
                        new ComicModel()
                        {
                            Id = "c-002",
                            Source = DefaultMockSource.Name,
                            Title = "Test Comic 2",
                        },
                    }
                )
            );

        // Act
        await _sut.LoadMoreCommand.ExecuteAsync(null);

        // Assert
        _sut.SearchedComics.Count.Should().Be(2);
        _sut.SearchedComics[1].Comic.Id.Should().Be("c-002");
    }

    [Fact]
    public async Task LoadMore_ShowWarning_WhenNoMoreResults()
    {
        // Arrange
        _sut.ComicSources = new List<ComicSourceModel>() { DefaultMockSource };
        _sut.SelectedComicSource = DefaultMockSource;

        _sut.SearchedComics = new ObservableCollection<ComicItemViewModel>
        {
            new ComicItemViewModel(
                new ComicModel
                {
                    Id = "c-001",
                    Source = DefaultMockSource.Name,
                    Title = "Test Comic",
                }
            ),
        };

        _comicServiceMock
            .Setup(c =>
                c.SearchComicsAsync(
                    DefaultMockSource.Name,
                    It.IsAny<string?>(),
                    It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(),
                    2,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<IReadOnlyList<ComicModel>>.Success(new List<ComicModel>()));

        // Act
        await _sut.LoadMoreCommand.ExecuteAsync(null);

        // Assert
        _notificationServiceMock.Verify(
            n => n.ShowWarning(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
        _sut.IsLoadingMore.Should().BeFalse();
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
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<IReadOnlyList<ComicModel>>.Failure("API error"));

        // Act
        await SetSelectedSourceAndWait("ErrorSource");

        // Assert
        _notificationServiceMock.Verify(
            n => n.ShowError("API error", It.IsAny<string>()),
            Times.Exactly(2)
        );
        _sut.IsContentLoading.Should().BeFalse();
    }

    // ────────────────────────────────────────────────────────────────
    // PROPERTY CHANGE EVENTS
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Changing_SelectedComicSource_ShouldLoadFilters_AndComics_WhenChanged()
    {
        // Arrange
        _comicServiceMock
            .Setup(s => s.GetSourceFiltersAsync(DefaultMockSource.Name))
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
                    DefaultMockSource.Name,
                    "",
                    It.IsAny<Dictionary<string, IReadOnlyList<string>>>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<IReadOnlyList<ComicModel>>.Success(
                    new List<ComicModel>
                    {
                        new ComicModel()
                        {
                            Id = "c-001",
                            Source = DefaultMockSource.Name,
                            Title = "Test Comic",
                        },
                    }
                )
            );

        // Act
        await SetSelectedSourceAndWait(DefaultMockSource.Name);

        // Assert
        _comicServiceMock.Verify(s => s.GetSourceFiltersAsync(DefaultMockSource.Name), Times.Once);
        _comicServiceMock.Verify(
            s =>
                s.SearchComicsAsync(
                    DefaultMockSource.Name,
                    "",
                    It.IsAny<Dictionary<string, IReadOnlyList<string>>>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        _sut.SearchedComics.Should().HaveCount(1);
        _sut.SearchedComics[0].Comic.Title.Should().Be("Test Comic");
        _sut.SearchedComics[0].Comic.Id.Should().Be("c-001");
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

        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Assert
        _sut.ComicSources.Should().HaveCount(1);
        _sut.ComicSources.Should().Contain(s => s.Name == "EnabledSource");
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
        await Task.Delay(100, TestContext.Current.CancellationToken);
    }
}
