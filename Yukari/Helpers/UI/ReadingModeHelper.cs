using Yukari.Enums;
using Yukari.Services.UI;

namespace Yukari.Helpers.UI;

public static class ReadingModeHelper
{
    public static string ToGlyph(ReadingMode mode) =>
        mode switch
        {
            ReadingMode.RightToLeft => "\uF0B0",
            ReadingMode.LeftToRight => "\uF0AF",
            ReadingMode.Vertical => "\uF0AE",
            ReadingMode.Webtoon => "\uF0AE",
            _ => string.Empty,
        };

    public static string ToDisplayName(ReadingMode mode)
    {
        var localization = App.GetService<ILocalizationService>();
        return mode switch
        {
            ReadingMode.RightToLeft => localization.GetString("ReadingMode/RightToLeft"),
            ReadingMode.LeftToRight => localization.GetString("ReadingMode/LeftToRight"),
            ReadingMode.Vertical => localization.GetString("ReadingMode/Vertical"),
            ReadingMode.Webtoon => localization.GetString("ReadingMode/Webtoon"),
            _ => string.Empty,
        };
    }
}
