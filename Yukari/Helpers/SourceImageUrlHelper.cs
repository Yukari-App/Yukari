using System;

internal static class SourceImageUrlHelper
{
    private const string Scheme = "source:///";

    public static string Encode(string sourceName, string url) =>
        $"{Scheme}{Uri.EscapeDataString(sourceName)}?url={Uri.EscapeDataString(url)}";

    public static bool TryDecode(string value, out string sourceName, out string url)
    {
        sourceName = string.Empty;
        url = string.Empty;

        if (!value.StartsWith(Scheme))
            return false;

        var withoutScheme = value[Scheme.Length..];
        var queryIndex = withoutScheme.IndexOf("?url=");
        if (queryIndex < 0)
            return false;

        sourceName = Uri.UnescapeDataString(withoutScheme[..queryIndex]);
        url = Uri.UnescapeDataString(withoutScheme[(queryIndex + 5)..]);
        return true;
    }
}
