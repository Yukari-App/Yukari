using Yukari.Enums;

namespace Yukari.Helpers.UI
{
    public static class ScalingModeHelper
    {
        public static string ToDisplayName(ScalingMode mode) =>
            mode switch
            {
                ScalingMode.FitScreen => "Fit Screen",
                ScalingMode.FitWidth => "Fit Width",
                ScalingMode.FitHeight => "Fit Height",
                ScalingMode.OriginalSize => "Original Size",
                _ => string.Empty,
            };
    }
}
