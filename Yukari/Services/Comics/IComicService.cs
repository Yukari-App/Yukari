using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Core.Models;
using Yukari.Models;
using Yukari.Models.DTO;

namespace Yukari.Services.Comics
{
    public interface IComicService
    {
        Task<IReadOnlyList<Filter>> GetSourceFiltersAsync(string? sourceName = null);
        Task<IReadOnlyDictionary<string, string>> GetSourceLanguagesAsync(string sourceName);

        Task<IReadOnlyList<ComicModel>> SearchComicsAsync(string sourceName, string? queryText, IReadOnlyDictionary<string, IReadOnlyList<string>> filters);
        Task<IReadOnlyList<ComicModel>> GetFavoriteComicsAsync(string? queryText, string filter);
        Task<ComicAggregate?> GetComicDetailsAsync(ContentKey ComicKey, bool forceWeb = false);
        Task<IReadOnlyList<ChapterAggregate>> GetAllChaptersAsync(ContentKey ComicKey, string language, bool forceWeb = false);
        Task<IReadOnlyList<ChapterPageModel>> GetChapterPagesAsync(ContentKey chapterKey);
        Task<IReadOnlyList<ComicSourceModel>> GetComicSourcesAsync();
    }
}
