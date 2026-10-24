using FluentAssertions;
using Yukari.Enums;
using Yukari.Helpers;

namespace Yukari.Tests.Helpers;

public class LocalComicHelperTests
{
    [Theory]
    [InlineData(LocalChapterFormat.Cbz, ".cbz")]
    [InlineData(LocalChapterFormat.Epub, ".epub")]
    [InlineData(LocalChapterFormat.FolderWithImages, null)]
    public void GetFileExtension_ShouldReturnExpectedExtension(
        LocalChapterFormat format,
        string? expectedExtension
    )
    {
        LocalComicHelper.GetFileExtension(format).Should().Be(expectedExtension);
    }

    [Fact]
    public void ChapterFileExtensions_ShouldContainAllFileBasedFormats()
    {
        LocalComicHelper
            .ChapterFileExtensions.Should()
            .Contain(new[] { ".cbz", ".epub" })
            .And.NotContain(string.Empty);
    }

    [Theory]
    [InlineData(@"C:\Manga\Chapter 01.cbz", true, LocalChapterFormat.Cbz)]
    [InlineData(@"C:\Manga\Chapter 01.CBZ", true, LocalChapterFormat.Cbz)]
    [InlineData(@"C:\Manga\Volume 01.epub", true, LocalChapterFormat.Epub)]
    [InlineData(@"C:\Manga\Volume 01.EPUB", true, LocalChapterFormat.Epub)]
    [InlineData(@"C:\Manga\Chapter 01.zip", false, default(LocalChapterFormat))]
    [InlineData(@"C:\Manga\Chapter 01", false, default(LocalChapterFormat))]
    [InlineData("", false, default(LocalChapterFormat))]
    [InlineData(null, false, default(LocalChapterFormat))]
    public void TryGetChapterFormatFromFile_ShouldDetectSupportedFormatsCaseInsensitively(
        string? filePath,
        bool expectedResult,
        LocalChapterFormat expectedFormat
    )
    {
        var result = LocalComicHelper.TryGetChapterFormatFromFile(filePath, out var format);

        result.Should().Be(expectedResult);
        if (expectedResult)
        {
            format.Should().Be(expectedFormat);
        }
    }

    [Theory]
    [InlineData(@"C:\Manga\Chapter 01.cbz", true)]
    [InlineData(@"C:\Manga\Volume 01.epub", true)]
    [InlineData(@"C:\Manga\Chapter 01.pdf", false)]
    [InlineData(null, false)]
    public void IsSupportedChapterFile_ShouldReturnExpectedResult(string? filePath, bool expected)
    {
        LocalComicHelper.IsSupportedChapterFile(filePath).Should().Be(expected);
    }
}
