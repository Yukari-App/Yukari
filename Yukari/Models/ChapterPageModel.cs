namespace Yukari.Models
{
    public class ChapterPageModel
    {
        public string? Id { get; set; }
        public string? ChapterId { get; set; }
        public required string Source { get; set; }
        public int PageNumber { get; set; }
        public required string ImageUrl { get; set; }
    }
}
