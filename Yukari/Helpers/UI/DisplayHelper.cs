using Yukari.Models;

namespace Yukari.Helpers.UI
{
    public static class DisplayHelper
    {
        public static string ToDisplayTitle(this ChapterModel chapter)
        {
            if (chapter == null) return string.Empty;

            var volume = !string.IsNullOrWhiteSpace(chapter.Volume) ? $"[{chapter.Volume}] " : string.Empty;
            var number = !string.IsNullOrWhiteSpace(chapter.Number) ? $"#{chapter.Number} " : "#N/A ";
            var title = chapter.Title ?? string.Empty;

            return $"{volume}{number}{title}".Trim();
        }
    }
}
