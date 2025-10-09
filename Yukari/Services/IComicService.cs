using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Enums;
using Yukari.Models;

namespace Yukari.Services
{
    internal interface IComicService
    {
        Task<List<ComicModel>> SearchComicsAsync(string? queryText, Dictionary<string, List<string>> filters);
        Task<List<ComicModel>> GetFavoriteComicsAsync(string? queryText, string filter);
        Task<ComicModel?> GetComicDetailsAsync(string id, ComicSourceType sourceType = ComicSourceType.Auto);
        Task<List<ChapterModel>> GetAllChaptersAsync(string comicId, string language, ComicSourceType sourceType = ComicSourceType.Auto);
        Task<ChapterModel> GetChapterAsync(string chapterId, ComicSourceType sourceType = ComicSourceType.Auto);
        Task<List<ChapterPageModel>> GetChapterPagesAsync(string chapterId, ComicSourceType sourceType = ComicSourceType.Auto);
        Task<List<ComicSourceModel>> GetComicSourcesAsync();
    }
}
