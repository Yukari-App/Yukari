using Yukari.Enums;
using Yukari.Services.UI;

namespace Yukari.Helpers.UI;

public static class ThemeModeHelper
{
    public static string ToDisplayName(ThemeMode mode)
    {
        var localization = App.GetService<ILocalizationService>();
        return mode switch
        {
            ThemeMode.Light => localization.GetString("ThemeMode/Light"),
            ThemeMode.Dark => localization.GetString("ThemeMode/Dark"),
            ThemeMode.System => localization.GetString("ThemeMode/System"),
            _ => string.Empty,
        };
    }
}
