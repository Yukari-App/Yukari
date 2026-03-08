using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using Moq;
using Xunit;
using Yukari.Models;
using Yukari.Services.Comics;
using Yukari.Services.UI;
using Yukari.ViewModels.Components;
using Yukari.ViewModels.Pages;

namespace Yukari.Tests.ViewModels
{
    public class DiscoverPageViewModelTests
    {
        private readonly Mock<IComicService> _comicServiceMock;
        private readonly Mock<IDialogService> _dialogServiceMock;
        private readonly Mock<INotificationService> _notificationServiceMock;
        private readonly Mock<IMessenger> _messengerMock;

        private readonly DiscoverPageViewModel _sut;

        public DiscoverPageViewModelTests()
        {
            _comicServiceMock = new Mock<IComicService>();
            _dialogServiceMock = new Mock<IDialogService>();
            _notificationServiceMock = new Mock<INotificationService>();
            _messengerMock = new Mock<IMessenger>();

            _sut = new DiscoverPageViewModel(
                _comicServiceMock.Object,
                _dialogServiceMock.Object,
                _notificationServiceMock.Object,
                _messengerMock.Object
            );
        }

        // ────────────────────────────────────────────────────────────────
        // Computed properties tests (what the UI shows)
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void NoSources_should_be_true_when_no_sources_and_not_loading()
        {
            // Arrange
            _sut.ComicSources = null;
            _sut.IsContentLoading = false;

            // Act & Assert
            _sut.NoSources.Should().BeTrue();
        }

        [Fact]
        public void NoSources_should_be_false_when_loading()
        {
            // Arrange
            _sut.IsContentLoading = true;

            // Act & Assert
            _sut.NoSources.Should().BeFalse();
        }

        [Fact]
        public void NoResults_should_be_true_when_has_sources_but_no_results_and_not_loading()
        {
            // Arrange
            _sut.ComicSources = new List<ComicSourceModel>
            {
                new ComicSourceModel()
                {
                    Name = "TestSource",
                    Version = "1.0.0+Core1.4.0",
                    DllPath = "Yukari.Plugin.TestSource.dll",
                },
            };
            _sut.SearchedComics = new List<ComicItemViewModel>();
            _sut.IsContentLoading = false;

            // Act & Assert
            _sut.NoResults.Should().BeTrue();
        }

        [Fact]
        public void NoResults_should_be_false_while_loading()
        {
            // Arrange
            _sut.IsContentLoading = true;

            // Act & Assert
            _sut.NoResults.Should().BeFalse();
        }

        // ────────────────────────────────────────────────────────────────
        // Command CanExecute tests (button enabled/disabled state)
        // ────────────────────────────────────────────────────────────────

        [Fact]
        public void FilterCommand_CanExecute_should_be_false_when_no_filters_available()
        {
            // Arrange
            _sut.IsContentLoading = false;

            // Act & Assert
            _sut.FilterCommand.CanExecute(null).Should().BeFalse();
        }

        [Fact]
        public void FilterCommand_CanExecute_should_be_false_during_loading()
        {
            // Arrange
            _sut.IsContentLoading = true;

            // Act & Assert
            _sut.FilterCommand.CanExecute(null).Should().BeFalse();
        }
    }
}
