using System;
using System.IO;

internal static class PageUriHelper
{
    public const string SourceScheme = "source:///";
    public const string ZipScheme = "zip:///";

    public static string EncodeSourceImage(string sourceName, string url) =>
        $"{SourceScheme}{Uri.EscapeDataString(sourceName)}?url={Uri.EscapeDataString(url)}";

    public static string EncodeZipEntry(string zipPath, string entryName) =>
        $"{ZipScheme}{Uri.EscapeDataString(zipPath)}#{Uri.EscapeDataString(entryName)}";

    public static bool TryDecodeSourceImage(
        string encoded,
        out string sourceName,
        out string url
    ) => TryDecode(encoded, SourceScheme, "?url=", out sourceName, out url);

    public static bool TryDecodeZipEntry(
        string encoded,
        out string zipPath,
        out string entryName
    ) => TryDecode(encoded, ZipScheme, "#", out zipPath, out entryName);

    public static string? GetFileExtension(string? encodedUrl)
    {
        if (string.IsNullOrWhiteSpace(encodedUrl))
            return null;

        if (TryDecodeSourceImage(encodedUrl, out _, out var realUrl))
            return Path.GetExtension(realUrl);
        if (TryDecodeZipEntry(encodedUrl, out _, out var entryName))
            return Path.GetExtension(entryName);
        return Path.GetExtension(encodedUrl);
    }

    private static bool TryDecode(
        string encoded,
        string scheme,
        string separator,
        out string part1,
        out string part2
    )
    {
        part1 = string.Empty;
        part2 = string.Empty;

        if (!encoded.StartsWith(scheme))
            return false;

        var withoutScheme = encoded[scheme.Length..];
        var sepIndex = withoutScheme.IndexOf(separator);
        if (sepIndex < 0)
            return false;

        part1 = Uri.UnescapeDataString(withoutScheme[..sepIndex]);
        part2 = Uri.UnescapeDataString(withoutScheme[(sepIndex + separator.Length)..]);
        return true;
    }
}
