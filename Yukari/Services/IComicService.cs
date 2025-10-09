using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Enums;
using Yukari.Models;

namespace Yukari.Services
{
    internal interface IComicService
    {
        Task<IReadOnlyList<ComicModel>> SearchComicsAsync(string? queryText, Dictionary<string, List<string>> filters);
        Task<IReadOnlyList<ComicModel>> GetFavoriteComicsAsync(string? queryText, string filter);
        Task<ComicModel?> GetComicDetailsAsync(string id, ComicSourceType sourceType = ComicSourceType.Auto);
        Task<IReadOnlyList<ChapterModel>> GetAllChaptersAsync(string comicId, string language, ComicSourceType sourceType = ComicSourceType.Auto);
        Task<IReadOnlyList<ChapterPageModel>> GetChapterPagesAsync(string chapterId, ComicSourceType sourceType = ComicSourceType.Auto);
        Task<IReadOnlyList<ComicSourceModel>> GetComicSourcesAsync();
    }
}
