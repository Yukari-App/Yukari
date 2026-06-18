using Yukari.Enums;

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

    public static string ToDisplayName(LocalChaptersFormat format) =>
        format switch
        {
            LocalChaptersFormat.FolderWithImages => "Folder",
            LocalChaptersFormat.Cbz => ".cbz",
            _ => string.Empty,
        };
}
