using System;

namespace Yukari.Helpers;

public static class ComicSourceExtensions
{
    public static bool IsCoreOutdated(this string? versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
            return false;

        var idx = versionString.IndexOf("+core", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return false;

        var verStr = versionString[(idx + "+core".Length)..];
        if (!Version.TryParse(verStr, out var required))
            return false;

        var current = new Version(
            AppInfoHelper.CoreVersion.Major,
            AppInfoHelper.CoreVersion.Minor,
            AppInfoHelper.CoreVersion.Build
        );

        var requiredNormalized = new Version(
            required.Major,
            required.Minor,
            required.Build >= 0 ? required.Build : 0
        );

        return requiredNormalized < current;
    }
}
