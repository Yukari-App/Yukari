using Yukari.Enums;
using Yukari.Services.UI;

namespace Yukari.Helpers.UI;

public static class LocalChaptersFormatHelper
{
    public static string ToGlyph(LocalChaptersFormat format) =>
        format switch
        {
            LocalChaptersFormat.FolderWithImages => "\uE8B7",
            LocalChaptersFormat.Cbz => "\uF012",
            _ => string.Empty,
        };

    public static string ToDisplayName(LocalChaptersFormat format)
    {
        var localization = App.GetService<ILocalizationService>();
        return format switch
        {
            LocalChaptersFormat.FolderWithImages => localization.GetString(
                "LocalChaptersFormat/FolderWithImages"
            ),
            LocalChaptersFormat.Cbz => ".cbz",
            _ => string.Empty,
        };
    }
}
