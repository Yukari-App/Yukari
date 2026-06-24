using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Moq;
using Yukari.Enums;
using Yukari.Messages;
using Yukari.Models;
using Yukari.Models.Common;
using Yukari.Models.DTO;
using Yukari.Models.Settings;
using Yukari.Services.Comics;
using Yukari.Services.Settings;
using Yukari.Services.UI;
using Yukari.Tests.TestUtils;
using Yukari.ViewModels.Components;
using Yukari.ViewModels.Pages;
using Yukari.Views.Pages;

namespace Yukari.Tests.ViewModels.Pages;

public class FavoritesPageViewModelTests
{
    private readonly Mock<IComicService> _comicServiceMock;
    private readonly Mock<ISettingsService> _settingsServiceMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly FakeMessenger _messenger;

    private readonly FavoritesPageViewModel _sut;

    public FavoritesPageViewModelTests()
    {
        _comicServiceMock = new Mock<IComicService>();
        _settingsServiceMock = new Mock<ISettingsService>();
        _dialogServiceMock = new Mock<IDialogService>();
        _notificationServiceMock = new Mock<INotificationService>();
        _messenger = new FakeMessenger();

        _settingsServiceMock.Setup(s => s.Current).Returns(new AppSettings());
        _comicServiceMock
            .Setup(s => s.GetCollectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string>()));
        _comicServiceMock
            .Setup(s =>
                s.GetFavoriteComicsAsync(
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<FavoritesSortBy>(),
                    It.IsAny<SortDirection>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<IReadOnlyList<ComicModel>>.Success(new List<ComicModel>()));

        _sut = new FavoritesPageViewModel(
            _comicServiceMock.Object,
            _settingsServiceMock.Object,
            _dialogServiceMock.Object,
            _notificationServiceMock.Object,
            _messenger
        );
    }

    private async Task InitializeSutAsync()
    {
        await _sut.InitializeAsync();
    }

    // ────────────────────────────────────────────────────────────────
    // PROPERTIES
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void NoFavorites_IsTrue_WhenNotLoading_AndNoFavorites()
    {
        // Arrange
        _sut.IsContentLoading = false;
        _sut.FavoriteComics = new();

        // Act & Assert
        _sut.NoFavorites.Should().BeTrue();
    }

    [Fact]
    public void NoFavorites_IsFalse_WhenLoading()
    {
        _sut.IsContentLoading = true;
        _sut.NoFavorites.Should().BeFalse();
    }

    [Fact]
    public void NoFavorites_IsFalse_WhenFavoritesExist()
    {
        _sut.IsContentLoading = false;
        _sut.FavoriteComics = new()
        {
            new ComicItemViewModel(
                new ComicModel
                {
                    Id = "1",
                    Source = "Src",
                    Title = "Test",
                },
                null!,
                null!
            ),
        };

        _sut.NoFavorites.Should().BeFalse();
    }

    [Fact]
    public void NoCollections_IsTrue_WhenCollectionsArrayIsEmpty()
    {
        _sut.Collections = Array.Empty<string>();
        _sut.NoCollections.Should().BeTrue();
    }

    [Fact]
    public void NoCollections_IsFalse_WhenCollectionsExist()
    {
        _sut.Collections = new[] { "Collection1" };
        _sut.NoCollections.Should().BeFalse();
    }

    // ────────────────────────────────────────────────────────────────
    // INITIALIZATION
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_LoadsSettings_AndFetchesCollectionsAndComics()
    {
        // Arrange
        var settings = new AppSettings
        {
            FavoritesSortBy = FavoritesSortBy.LastRead,
            FavoritesSortDirection = SortDirection.Descending,
        };
        _settingsServiceMock.Setup(s => s.Current).Returns(settings);
        _comicServiceMock
            .Setup(s => s.GetCollectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new[] { "Collection1" }));
        _comicServiceMock
            .Setup(s =>
                s.GetFavoriteComicsAsync(
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    FavoritesSortBy.LastRead,
                    SortDirection.Descending,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<IReadOnlyList<ComicModel>>.Success(
                    new List<ComicModel>
                    {
                        new()
                        {
                            Id = "1",
                            Source = "Src",
                            Title = "Test Comic",
                        },
                    }
                )
            );

        // Act
        await _sut.InitializeAsync();

        // Assert
        _sut.SortBy.Should().Be(FavoritesSortBy.LastRead);
        _sut.SortDirection.Should().Be(SortDirection.Descending);
        _sut.Collections.Should().Contain("Collection1");
        _sut.FavoriteComics.Should().HaveCount(1);
        _sut.FavoriteComics[0].Comic.Title.Should().Be("Test Comic");
        _sut.IsContentLoading.Should().BeFalse();
    }

    // ────────────────────────────────────────────────────────────────
    // MESSAGE HANDLING
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Receive_SearchChangedMessage_ShouldUpdateDisplayedComics()
    {
        // Arrange
        await InitializeSutAsync();
        _comicServiceMock
            .Setup(s =>
                s.GetFavoriteComicsAsync(
                    "test search",
                    It.IsAny<string?>(),
                    It.IsAny<FavoritesSortBy>(),
                    It.IsAny<SortDirection>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<IReadOnlyList<ComicModel>>.Success(
                    new List<ComicModel>
                    {
                        new()
                        {
                            Id = "2",
                            Source = "Src",
                            Title = "Searched Comic",
                        },
                    }
                )
            );

        // Act
        _sut.Receive(new SearchChangedMessage("test search"));
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Assert
        _sut.FavoriteComics.Should().HaveCount(1);
        _sut.FavoriteComics[0].Comic.Title.Should().Be("Searched Comic");
    }

    // ────────────────────────────────────────────────────────────────
    // COMMANDS EXECUTION
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void ToggleShowAllCollections_SelectsNull_WhenShowAllIsTrue()
    {
        // Arrange
        _sut.Collections = new[] { "C1", "C2" };
        _sut.SelectedCollection = "C1";

        // Act
        _sut.ShowAllCollections = true; // Simulate TwoWay binding
        _sut.ToggleShowAllCollectionsCommand.Execute(null);

        // Assert
        _sut.SelectedCollection.Should().BeNull();
    }

    [Fact]
    public void ToggleShowAllCollections_SelectsFirstCollection_WhenShowAllIsFalse()
    {
        _sut.ShowAllCollections = false;
        _sut.Collections = new[] { "C1", "C2" };
        _sut.ToggleShowAllCollectionsCommand.Execute(null);
        _sut.SelectedCollection.Should().Be("C1");
    }

    [Fact]
    public async Task OpenCollectionManager_ShowsDialog_AndRefreshes()
    {
        // Arrange
        await InitializeSutAsync();
        _dialogServiceMock.Setup(d => d.ShowCollectionsManagerAsync()).Returns(Task.CompletedTask);
        _comicServiceMock
            .Setup(s => s.GetCollectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new[] { "NewCollection" }));

        // Act
        await _sut.OpenCollectionManagerCommand.ExecuteAsync(null);

        // Assert
        _dialogServiceMock.Verify(d => d.ShowCollectionsManagerAsync(), Times.Once);
        _sut.Collections.Should().Contain("NewCollection");
    }

    [Fact]
    public async Task SetSortBy_ChangesSortBy_AndResetsSortDirection()
    {
        // Arrange
        _sut.SortDirection = SortDirection.Descending;
        await InitializeSutAsync();
        _comicServiceMock.Invocations.Clear();

        // Act
        _sut.SetSortByCommand.Execute("LastRead");

        // Assert
        _sut.SortBy.Should().Be(FavoritesSortBy.LastRead);
        _sut.SortDirection.Should().Be(SortDirection.Descending);
        _settingsServiceMock.Verify(
            s => s.Set(s => s.FavoritesSortBy, FavoritesSortBy.LastRead),
            Times.Once
        );
        _comicServiceMock.Verify(
            s =>
                s.GetFavoriteComicsAsync(
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    FavoritesSortBy.LastRead,
                    It.IsAny<SortDirection>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.AtLeastOnce
        );
    }

    [Fact]
    public async Task SetSortDirection_ChangesSortDirection_AndRefreshes()
    {
        // Arrange
        await InitializeSutAsync();
        _comicServiceMock.Invocations.Clear();

        // Act
        _sut.SetSortDirectionCommand.Execute("Descending");

        // Assert
        _sut.SortDirection.Should().Be(SortDirection.Descending);
        _settingsServiceMock.Verify(
            s => s.Set(s => s.FavoritesSortDirection, SortDirection.Descending),
            Times.Once
        );
    }

    [Fact]
    public async Task RemoveFavoriteComic_Removes_AndRefreshes_WhenSuccess()
    {
        // Arrange
        var comicKey = new ContentKey("1", "Src");
        _comicServiceMock
            .Setup(s => s.RemoveFavoriteComicAsync(comicKey))
            .ReturnsAsync(Result.Success());
        await InitializeSutAsync();

        // Act
        await _sut.RemoveFavoriteComicCommand.ExecuteAsync(comicKey);

        // Assert
        _comicServiceMock.Verify(s => s.RemoveFavoriteComicAsync(comicKey), Times.Once);
        _comicServiceMock.Verify(
            s =>
                s.GetFavoriteComicsAsync(
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<FavoritesSortBy>(),
                    It.IsAny<SortDirection>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.AtLeastOnce
        );
    }

    [Fact]
    public async Task RemoveFavoriteComic_ShowsError_WhenFailure()
    {
        // Arrange
        var comicKey = new ContentKey("1", "Src");
        _comicServiceMock
            .Setup(s => s.RemoveFavoriteComicAsync(comicKey))
            .ReturnsAsync(Result.Failure("Error", "Title"));
        await InitializeSutAsync();

        // Act
        await _sut.RemoveFavoriteComicCommand.ExecuteAsync(comicKey);

        // Assert
        _notificationServiceMock.Verify(n => n.ShowError("Error", "Title"), Times.Once);
    }

    [Fact]
    public async Task OpenLocalComicManager_ShowsDialog_AndRefreshes()
    {
        // Arrange
        await InitializeSutAsync();
        _dialogServiceMock
            .Setup(d => d.ShowLocalComicDialogAsync(It.IsAny<ContentKey?>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.OpenLocalComicManagerCommand.ExecuteAsync(null);

        // Assert
        _dialogServiceMock.Verify(d => d.ShowLocalComicDialogAsync(null), Times.Once);
        _comicServiceMock.Verify(
            s =>
                s.GetFavoriteComicsAsync(
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<FavoritesSortBy>(),
                    It.IsAny<SortDirection>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.AtLeastOnce
        );
    }

    [Fact]
    public void NavigateToComic_SendsNavigateMessage()
    {
        var comicKey = new ContentKey("1", "Src");
        _sut.NavigateToComicCommand.Execute(comicKey);

        var message = _messenger.GetSingleSentMessage<NavigateMessage>();
        message.Should().NotBeNull();
        message.PageType.Should().Be<ComicPage>();
        message.Parameter.Should().Be(comicKey);
    }
}
