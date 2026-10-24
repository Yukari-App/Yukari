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
        if (LocalComicHelper.GetFileExtension(format) is string ext)
            return ext;

        var localization = App.GetService<ILocalizationService>();
        return format switch
        {
            LocalChapterFormat.FolderWithImages => localization.GetString(
                "LocalChapterFormat/FolderWithImages"
            ),
            _ => string.Empty,
        };
    }
}
