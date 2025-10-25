using System;

namespace Yukari.Models
{
    public class ChapterModel
    {
        public required string Id { get; set; }
        public required string ComicId { get; set; }
        public required string Source { get; set; }
        public string? Title { get; set; }
        public required string Number { get; set; }
        public string? Volume { get; set; }
        public required string Language { get; set; }
        public string? Groups { get; set; }
        public DateOnly LastUpdate { get; set; }
        public int Pages { get; set; }
        public int? LastPageRead { get; set; }
        public bool? IsDownloaded { get; set; }
        public bool? IsRead { get; set; }
    }
}
