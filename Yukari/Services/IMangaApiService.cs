using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Models;

namespace Yukari.Services
{
    public interface IMangaApiService
    {
        Task<List<Manga>> SearchMangasAsync(string query);
        Task<List<Manga>> GetTrendingMangasAsync();
        Task<Manga?> GetMangaByIdAsync(Guid id);
        Task<MangaChapter> GetMangaChapterAsync(Guid chapterId);
        Task<List<MangaChapter>> GetAllMangaChaptersAsync(Guid mangaId);
    }
}
