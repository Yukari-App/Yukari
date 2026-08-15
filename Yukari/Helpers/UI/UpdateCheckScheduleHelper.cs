using Yukari.Enums;
using Yukari.Services.UI;

namespace Yukari.Helpers.UI;

public static class UpdateCheckScheduleHelper
{
    public static string ToDisplayName(UpdateCheckSchedule schedule)
    {
        var localization = App.GetService<ILocalizationService>();
        return schedule switch
        {
            UpdateCheckSchedule.Never => localization.GetString("UpdateCheckSchedule/Never"),
            UpdateCheckSchedule.ThreeHours => localization.GetString(
                "UpdateCheckSchedule/ThreeHours"
            ),
            UpdateCheckSchedule.SixHours => localization.GetString("UpdateCheckSchedule/SixHours"),
            UpdateCheckSchedule.Daily => localization.GetString("UpdateCheckSchedule/Daily"),
            UpdateCheckSchedule.Weekly => localization.GetString("UpdateCheckSchedule/Weekly"),
            _ => string.Empty,
        };
    }
}
