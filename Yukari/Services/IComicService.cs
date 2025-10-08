using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Enums;
using Yukari.Models;

namespace Yukari.Services
{
    internal interface IComicService
    {
        Task<List<ComicModel>> SearchComicsAsync(string? queryText = null, ComicSourceType sourceType = ComicSourceType.Auto);
        Task<List<ComicModel>> GetFavoriteComicsAsync(string? queryText = null);
        Task<ComicModel?> GetComicDetailsAsync(string id, ComicSourceType sourceType = ComicSourceType.Auto);
        Task<List<ChapterModel>> GetAllChaptersAsync(string comicId, ComicSourceType sourceType = ComicSourceType.Auto);
        Task<ChapterModel> GetChapterAsync(string chapterId, ComicSourceType sourceType = ComicSourceType.Auto);
        Task<List<ChapterPageModel>> GetChapterPagesAsync(string chapterId, ComicSourceType sourceType = ComicSourceType.Auto);
        Task<List<ComicSourceModel>> GetComicSourcesAsync();
    }
}
