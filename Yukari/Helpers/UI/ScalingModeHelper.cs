using Yukari.Enums;
using Yukari.Services.UI;

namespace Yukari.Helpers.UI;

public static class ScalingModeHelper
{
    public static string ToDisplayName(ScalingMode mode)
    {
        var localization = App.GetService<ILocalizationService>();
        return mode switch
        {
            ScalingMode.FitScreen => localization.GetString("ScalingMode/FitScreen"),
            ScalingMode.FitWidth => localization.GetString("ScalingMode/FitWidth"),
            ScalingMode.FitHeight => localization.GetString("ScalingMode/FitHeight"),
            ScalingMode.OriginalSize => localization.GetString("ScalingMode/OriginalSize"),
            _ => string.Empty,
        };
    }
}
