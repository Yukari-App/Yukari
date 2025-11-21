using System.Collections.Generic;

namespace Yukari.Models.Data
{
    public class ComicUserData
    {
        public List<string> DownloadedLangs { get; set; } = new List<string>();
        public string? LastSelectedLang { get; set; }
        public bool IsFavorite { get; set; }

        public bool IsLangDownloaded(string lang) => DownloadedLangs.Contains(lang);
    }
}
