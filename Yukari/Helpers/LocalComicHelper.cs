using System;
using System.IO;
using Yukari.Enums;

namespace Yukari.Helpers;

internal static class LocalComicHelper
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
