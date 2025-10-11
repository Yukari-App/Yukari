namespace Yukari.Models
{
    public class ChapterPageModel
    {
        public string? Id { get; set; }
        public string? ChapterId { get; set; }
        public string Source { get; set; }
        public int PageNumber { get; set; }
        public string ImageUrl { get; set; }
    }
}
