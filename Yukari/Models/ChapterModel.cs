using System;

namespace Yukari.Models
{
    public class ChapterModel
    {
        public string Id { get; set; }
        public string ComicId { get; set; }
        public string Source { get; set; }
        public string? Title { get; set; }
        public string Number { get; set; }
        public string Volume { get; set; }
        public string Language { get; set; }
        public string? Groups { get; set; }
        public DateOnly LastUpdate { get; set; }
        public int Pages { get; set; }
        public int? LastPageRead { get; set; }
        public bool? IsDownloaded { get; set; }
        public bool? IsRead { get; set; }
    }
}
