using System;

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
        public string[] Tags { get; set; } = new[] { "N/A" };
        public int? Year { get; set; }
        public string? CoverImageUrl { get; set; }
        public LanguageModel[] Langs { get; set; } = Array.Empty<LanguageModel>();

        public bool IsAvailable { get; set; } = true;
    }
}
