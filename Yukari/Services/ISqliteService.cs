using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Models;

namespace Yukari.Services
{
    internal interface ISqliteService
    {
        Task<IReadOnlyList<ComicModel>> GetFavoriteComicsAsync(string? queryText = null);
        Task<ComicModel?> GetComicDetailsAsync(string comicId);
        Task<IReadOnlyList<ChapterModel>> GetAllChaptersAsync(string mangaId, string language);
        Task<IReadOnlyList<ChapterPageModel>> GetChapterPagesAsync(string chapterId);

        Task<IReadOnlyList<ComicSourceModel>> GetComicSourcesAsync();
        ComicSourceModel? GetComicSourceDetails(string sourceName);
    }
}
