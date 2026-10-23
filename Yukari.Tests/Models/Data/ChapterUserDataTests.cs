using FluentAssertions;
using Yukari.Models.Data;

namespace Yukari.Tests.Models.Data;

public class ChapterUserDataTests
{
    [Theory]
    [InlineData(true, null, true)]
    [InlineData(true, 0, true)]
    [InlineData(false, 3, true)]
    [InlineData(false, 0, false)]
    [InlineData(false, null, false)]
    public void HasProgress_ReturnsExpectedValue(bool isRead, int? lastPageRead, bool expected)
    {
        // Arrange
        var sut = new ChapterUserData { IsRead = isRead, LastPageRead = lastPageRead };

        // Act & Assert
        sut.HasProgress.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, 5, false, 0)]
    [InlineData(false, 5, true, 0)]
    [InlineData(false, null, false, 0)]
    [InlineData(false, 0, false, 0)]
    [InlineData(false, 5, false, 4)]
    public void ResumePageIndex_ReturnsExpectedIndex(
        bool isRead,
        int? lastPageRead,
        bool forceFirstPage,
        int expected
    )
    {
        // Arrange
        var sut = new ChapterUserData { IsRead = isRead, LastPageRead = lastPageRead };

        // Act
        var result = sut.ResumePageIndex(forceFirstPage);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, 3, 10, 10)]
    [InlineData(true, null, null, 0)]
    [InlineData(false, 5, 10, 5)]
    [InlineData(false, null, 10, 0)]
    public void DisplayLastPage_ReturnsExpectedPage(
        bool isRead,
        int? lastPageRead,
        int? totalPages,
        int expected
    )
    {
        // Arrange
        var sut = new ChapterUserData { IsRead = isRead, LastPageRead = lastPageRead };

        // Act
        var result = sut.DisplayLastPage(totalPages);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void RecordProgress_DoesNotChangeValues_WhenAlreadyRead()
    {
        // Arrange
        var sut = new ChapterUserData { IsRead = true, LastPageRead = 10 };

        // Act
        sut.RecordProgress(3, 10);

        // Assert
        sut.IsRead.Should().BeTrue();
        sut.LastPageRead.Should().Be(10);
    }

    [Fact]
    public void RecordProgress_MarksAsRead_WhenOnLastPage()
    {
        // Arrange
        var sut = new ChapterUserData { IsRead = false };

        // Act
        sut.RecordProgress(10, 10);

        // Assert
        sut.LastPageRead.Should().Be(10);
        sut.IsRead.Should().BeTrue();
    }

    [Fact]
    public void RecordProgress_DoesNotMarkAsRead_WhenOnIntermediatePage()
    {
        // Arrange
        var sut = new ChapterUserData { IsRead = false };

        // Act
        sut.RecordProgress(5, 10);

        // Assert
        sut.LastPageRead.Should().Be(5);
        sut.IsRead.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(null)]
    public void RecordProgress_SetsZeroAndDoesNotMarkAsRead_WhenChapterHasNoPages(int? totalPages)
    {
        // Arrange
        var sut = new ChapterUserData { IsRead = false };

        // Act
        sut.RecordProgress(0, totalPages);

        // Assert
        sut.LastPageRead.Should().Be(0);
        sut.IsRead.Should().BeFalse();
    }
}
