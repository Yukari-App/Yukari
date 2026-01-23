using System;

namespace Yukari.Models
{
    public class ChapterModel
    {
        public required string Id { get; set; }
        public required string ComicId { get; set; }
        public required string Source { get; set; }
        public string? Title { get; set; }
        public string? Number { get; set; }
        public string? Volume { get; set; }
        public required string Language { get; set; }
        public string Groups { get; set; } = "N/A";
        public DateOnly LastUpdate { get; set; }
        public int Pages { get; set; }

        public bool IsAvailable { get; set; } = true;
    }
}
