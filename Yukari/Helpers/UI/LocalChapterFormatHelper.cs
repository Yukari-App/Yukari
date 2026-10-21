using Yukari.Enums;
using Yukari.Services.UI;

namespace Yukari.Helpers.UI;

public static class LocalChapterFormatHelper
{
    public static string ToGlyph(LocalChapterFormat format) =>
        format switch
        {
            LocalChapterFormat.FolderWithImages => "\uE8B7",
            LocalChapterFormat.Cbz => "\uF012",
            LocalChapterFormat.Epub => "\uE736",
            _ => string.Empty,
        };

    public static string ToDisplayName(LocalChapterFormat format)
    {
        var localization = App.GetService<ILocalizationService>();
        return format switch
        {
            LocalChapterFormat.FolderWithImages => localization.GetString(
                "LocalChapterFormat/FolderWithImages"
            ),
            LocalChapterFormat.Cbz => ".cbz",
            LocalChapterFormat.Epub => ".epub",
            _ => string.Empty,
        };
    }
}
