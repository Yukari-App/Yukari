using Yukari.Enums;

namespace Yukari.Helpers.UI;

public static class ReadingModeHelper
{
    public static string ToGlyph(ReadingMode mode) =>
        mode switch
        {
            ReadingMode.RightToLeft => "\uF0B0",
            ReadingMode.LeftToRight => "\uF0AF",
            ReadingMode.Vertical => "\uF0AE",
            _ => string.Empty,
        };

    public static string ToDisplayName(ReadingMode mode) =>
        mode switch
        {
            ReadingMode.RightToLeft => "Right to Left",
            ReadingMode.LeftToRight => "Left to Right",
            ReadingMode.Vertical => "Vertical",
            _ => string.Empty,
        };
}
