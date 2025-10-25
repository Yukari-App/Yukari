using System;
using System.Collections.Generic;

namespace Yukari.Models
{
    public class ComicModel
    {
        public required string Id { get; set; }
        public required string Source { get; set; }
        public string? ComicUrl { get; set; }
        public required string Title { get; set; }
        public string? Author { get; set; }
        public string? Description { get; set; }
        public string[] Tags { get; set; } = Array.Empty<string>();
        public int? Year { get; set; }
        public string? CoverImageUrl { get; set; }
        public string[] Langs { get; set; } = Array.Empty<string>();
        public List<string> DownloadedLangs { get; set; } = new List<string>();
        public string? LastSelectedLang { get; set; }
        public bool IsFavorite { get; set; }

        public bool IsLangDownloaded(string lang) => DownloadedLangs.Contains(lang);
    }
}
