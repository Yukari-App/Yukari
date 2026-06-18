using Yukari.Models;

namespace Yukari.Helpers.UI;

public static class DisplayHelper
{
    public static string ToDisplayTitle(this ChapterModel chapter)
    {
        if (chapter == null)
            return string.Empty;

        var volume = !string.IsNullOrWhiteSpace(chapter.Volume) ? $"[{chapter.Volume}] " : "";
        var number = !string.IsNullOrWhiteSpace(chapter.Number) ? $"#{chapter.Number} " : "";
        var title = chapter.Title ?? "";

        var display = $"{volume}{number}{title}".Trim();

        if (string.IsNullOrEmpty(display))
            display = $"Chapter {chapter.Id}";

        return display;
    }
}
