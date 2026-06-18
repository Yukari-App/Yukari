using System;
using Yukari.Enums;

namespace Yukari.Helpers;

internal static class LocalComicConstants
{
    public const string SourceName = "Local";

    public static string EncodeChaptersPath(string path, LocalChaptersFormat format) =>
        $"{format.ToString().ToLowerInvariant()}|{path}";

    public static (string Path, LocalChaptersFormat Format) DecodeChaptersPath(string encoded)
    {
        var parts = encoded.Split('|', 2);
        var format = Enum.Parse<LocalChaptersFormat>(parts[0], ignoreCase: true);
        return (parts[1], format);
    }
}
