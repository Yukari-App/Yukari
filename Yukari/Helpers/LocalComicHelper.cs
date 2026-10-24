using System;
using System.IO;
using System.Linq;
using Yukari.Enums;

namespace Yukari.Helpers;

public static class LocalComicHelper
{
    public const string SourceName = "Local";

    public static readonly string[] CoverExtensions = new[]
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".bmp",
        ".webp",
        ".svg",
        ".svgz",
    };

    public static readonly string[] PageImageExtensions = new[]
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".bmp",
        ".webp",
    };

    public static readonly string[] ChapterFileExtensions = Enum.GetValues<LocalChapterFormat>()
        .Select(GetFileExtension)
        .OfType<string>()
        .ToArray();

    public static string? GetFileExtension(LocalChapterFormat format) =>
        format switch
        {
            LocalChapterFormat.Cbz => ".cbz",
            LocalChapterFormat.Epub => ".epub",
            _ => null,
        };

    public static bool TryGetChapterFormatFromFile(string? filePath, out LocalChapterFormat format)
    {
        format = default;
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        var ext = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(ext))
            return false;

        foreach (var candidate in Enum.GetValues<LocalChapterFormat>())
        {
            var candidateExt = GetFileExtension(candidate);
            if (
                candidateExt != null
                && string.Equals(ext, candidateExt, StringComparison.OrdinalIgnoreCase)
            )
            {
                format = candidate;
                return true;
            }
        }

        return false;
    }

    public static bool IsSupportedChapterFile(string? filePath) =>
        TryGetChapterFormatFromFile(filePath, out _);

    public static string EncodeChaptersPath(string path, LocalChapterFormat format) =>
        $"{format.ToString().ToLowerInvariant()}|{path}";

    public static (string Path, LocalChapterFormat Format) DecodeChaptersPath(string encoded)
    {
        var parts = encoded.Split('|', 2);
        var format = Enum.Parse<LocalChapterFormat>(parts[0], ignoreCase: true);
        return (parts[1], format);
    }

    public static bool IsPageImageFile(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return Array.IndexOf(PageImageExtensions, ext) >= 0;
    }
}
