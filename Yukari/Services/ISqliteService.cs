using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Models;

namespace Yukari.Services
{
    public interface ISqliteService
    {
        Task<Manga?> GetMangaByIdAsync(Guid id);
        Task<List<Manga>> GetFavoriteMangasAsync();
        Task<MangaChapter> GetMangaChapterAsync(Guid chapterId);
        Task<List<MangaChapter>> GetAllMangaChaptersAsync(Guid mangaId);
        Task<List<Manga>> SearchFavoriteMangasAsync(string? queryText);
        Task<List<Manga>> SearchMangasAsync(string? queryText);
    }
}
