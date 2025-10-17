using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Core.Models;
using Yukari.Enums;
using Yukari.Models;

namespace Yukari.Services.Comics
{
    public interface IComicService
    {
        Task<IReadOnlyList<Filter>> GetSourceFiltersAsync(string? sourceName = null);
        Task<IReadOnlyDictionary<string, string>> GetSourceLanguagesAsync(string sourceName);

        Task<IReadOnlyList<ComicModel>> SearchComicsAsync(string sourceName, string? queryText, Dictionary<string, List<string>> filters);
        Task<IReadOnlyList<ComicModel>> GetFavoriteComicsAsync(string? queryText, string filter);
        Task<ComicModel?> GetComicDetailsAsync(ContentIdentifier comicIdentifier, ComicSourceType sourceType = ComicSourceType.Auto);
        Task<IReadOnlyList<ChapterModel>> GetAllChaptersAsync(ContentIdentifier comicIdentifier, string language, ComicSourceType sourceType = ComicSourceType.Auto);
        Task<IReadOnlyList<ChapterPageModel>> GetChapterPagesAsync(ContentIdentifier chapterIdentifier, ComicSourceType sourceType = ComicSourceType.Auto);
        Task<IReadOnlyList<ComicSourceModel>> GetComicSourcesAsync();
    }
}
