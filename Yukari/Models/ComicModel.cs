namespace Yukari.Models
{
    internal class ComicModel
    {
        public string Id { get; set; }
        public string Source { get; set; }
        public string? ComicUrl { get; set; }
        public string Title { get; set; }
        public string? Author { get; set; }
        public string? Description { get; set; }
        public string[] Tags { get; set; }
        public int? Year { get; set; }
        public string? CoverImageUrl { get; set; }
        public string[] Langs { get; set; }
        public string? LastSelectedLang { get; set; }
        public bool IsFavorite { get; set; }
    }
}
