using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Models;

namespace Yukari.Services
{
    public interface ISqliteService
    {
        Task<Manga?> GetMangaByIdAsync(Guid id);
        Task<List<Manga>> GetMangasAsync(string? queryText = null);
        Task<List<Manga>> GetFavoriteMangasAsync(string? queryText = null);
        Task<MangaChapter> GetMangaChapterAsync(Guid chapterId);
        Task<List<MangaChapter>> GetAllMangaChaptersAsync(Guid mangaId);
        Task<List<MangaSourceModel>> GetMangaSourcesAsync();
    }
}
