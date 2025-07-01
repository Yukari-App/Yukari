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
        public string _chapterVolume;
        public string _groups;
        public DateOnly _chapterRelease;
        public int _chapterNumber;
        public int _chapterPagesNumber;
        public int _lastPageReaded;
        public bool IsDownloaded;
        public bool IsRead;
    }

    public class MangaPage
    {
        public Guid Id { get; set; }
        public Guid ChapterId { get; set; }
        public int PageNumber { get; set; }
        public string ImageUrl { get; set; }
    }
}
