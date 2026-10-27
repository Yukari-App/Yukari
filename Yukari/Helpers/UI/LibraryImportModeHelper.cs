using Yukari.Enums;
using Yukari.Services.UI;

namespace Yukari.Helpers.UI;

public static class LibraryImportModeHelper
{
    public static string ToGlyph(LibraryImportMode mode) =>
        mode switch
        {
            LibraryImportMode.Merge => "\uEA3C",
            LibraryImportMode.Replace => "\uE8AB",
            _ => string.Empty,
        };

    public static string ToDisplayName(LibraryImportMode mode)
    {
        var localization = App.GetService<ILocalizationService>();
        return mode switch
        {
            LibraryImportMode.Merge => localization.GetString("LibraryImportMode/Merge"),
            LibraryImportMode.Replace => localization.GetString("LibraryImportMode/Replace"),
            _ => string.Empty,
        };
    }
}
