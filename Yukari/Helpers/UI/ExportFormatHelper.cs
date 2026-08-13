using Yukari.Enums;
using Yukari.Services.UI;

namespace Yukari.Helpers.UI;

public static class ExportFormatHelper
{
    public static string ToGlyph(ExportFormat format) =>
        format switch
        {
            ExportFormat.FolderWithImages => "\uE8B7",
            ExportFormat.Cbz => "\uF012",
            _ => string.Empty,
        };

    public static string ToDisplayName(ExportFormat format)
    {
        var localization = App.GetService<ILocalizationService>();
        return format switch
        {
            ExportFormat.FolderWithImages => localization.GetString(
                "ExportFormat/FolderWithImages"
            ),
            ExportFormat.Cbz => ".cbz",
            _ => string.Empty,
        };
    }
}
