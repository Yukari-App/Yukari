using FluentAssertions;
using Yukari.Helpers;

namespace Yukari.Tests.Helpers;

public sealed partial class FileActivationHelperTests : IDisposable
{
    private const string ExePath = @"C:\Program Files\Yukari\Yukari.exe";

    private readonly string _directory;

    public FileActivationHelperTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"yukari-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Theory]
    [InlineData("My Chapter 01.cbz")]
    [InlineData("Volume.epub")]
    [InlineData("UPPERCASE.CBZ")]
    public void TryGetChapterFilePath_ShouldReturnFile_WhenCommandLineContainsExistingSupportedFile(
        string fileName
    )
    {
        // Arrange
        var filePath = CreateFile(fileName);

        // Act
        var result = FileActivationHelper.TryGetChapterFilePath($"\"{ExePath}\" \"{filePath}\"");

        // Assert
        result.Should().Be(filePath);
    }

    [Fact]
    public void TryGetChapterFilePath_ShouldReturnFile_WhenPathIsNotQuoted()
    {
        // Arrange
        var filePath = CreateFile("chapter.cbz");

        // Act
        var result = FileActivationHelper.TryGetChapterFilePath(
            $"{ExePath.Replace(" ", "")} {filePath}"
        );

        // Assert
        result.Should().Be(filePath);
    }

    [Fact]
    public void TryGetChapterFilePath_ShouldReturnFirstValidFile_WhenMultipleArgumentsAreProvided()
    {
        // Arrange
        var first = CreateFile("first.cbz");
        var second = CreateFile("second.cbz");

        // Act
        var result = FileActivationHelper.TryGetChapterFilePath(
            $"\"{ExePath}\" \"{Path.Combine(_directory, "missing.cbz")}\" \"{first}\" \"{second}\""
        );

        // Assert
        result.Should().Be(first);
    }

    [Fact]
    public void TryGetChapterFilePath_ShouldReturnNull_WhenFileDoesNotExist()
    {
        var missing = Path.Combine(_directory, "missing.cbz");

        FileActivationHelper
            .TryGetChapterFilePath($"\"{ExePath}\" \"{missing}\"")
            .Should()
            .BeNull();
    }

    [Fact]
    public void TryGetChapterFilePath_ShouldReturnNull_WhenExtensionIsNotSupported()
    {
        var filePath = CreateFile("document.pdf");

        FileActivationHelper
            .TryGetChapterFilePath($"\"{ExePath}\" \"{filePath}\"")
            .Should()
            .BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"""C:\Program Files\Yukari\Yukari.exe""")]
    public void TryGetChapterFilePath_ShouldReturnNull_WhenThereIsNoFileArgument(
        string? commandLine
    )
    {
        FileActivationHelper.TryGetChapterFilePath(commandLine).Should().BeNull();
    }

    private string CreateFile(string fileName)
    {
        var filePath = Path.Combine(_directory, fileName);
        File.WriteAllText(filePath, string.Empty);
        return filePath;
    }
}
