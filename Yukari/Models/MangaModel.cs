using System;


namespace Yukari.Models
{
    public class Manga
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public string CoverImageUrl { get; set; }
    }

    public class MangaChapter
    {
        public Guid Id { get; set; }
        public Guid MangaId { get; set; }
        public string Title { get; set; }
        public string ChapterUrl { get; set; }
        public int _chapterNumber { get; set; }
        public string _chapterVolume { get; set; }
        public string _groups { get; set; }
        public DateOnly _chapterRelease { get; set; }
        public int _chapterPagesNumber { get; set; }
        public int _lastPageReaded { get; set; }
        public bool IsDownloaded { get; set; }
        public bool IsRead { get; set; }
    }

    public class MangaPage
    {
        public Guid Id { get; set; }
        public Guid ChapterId { get; set; }
        public int PageNumber { get; set; }
        public string ImageUrl { get; set; }
    }
}
