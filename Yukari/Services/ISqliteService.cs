using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Models;

namespace Yukari.Services
{
    internal interface ISqliteService
    {
        Task<List<ComicModel>> GetFavoriteComicsAsync(string? queryText = null);
        Task<ComicModel?> GetComicDetailsAsync(string comicId);
        Task<List<ChapterModel>> GetAllChaptersAsync(string mangaId);
        Task<List<ChapterPageModel>> GetChapterPagesAsync(string chapterId);

        Task<List<ComicSourceModel>> GetComicSourcesAsync();
        ComicSourceModel? GetComicSourceDetails(string sourceName);
    }
}
