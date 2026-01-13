using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Core.Models;
using Yukari.Models;
using Yukari.Models.Common;
using Yukari.Models.Data;
using Yukari.Models.DTO;

namespace Yukari.Services.Comics
{
    public interface IComicService
    {
        Task<IReadOnlyList<Filter>> GetSourceFiltersAsync(string sourceName);
        Task<IReadOnlyDictionary<string, string>> GetSourceLanguagesAsync(string sourceName);

        Task<IReadOnlyList<ComicModel>> SearchComicsAsync(string sourceName, string? queryText, IReadOnlyDictionary<string, IReadOnlyList<string>> filters);
        Task<IReadOnlyList<ComicModel>> GetFavoriteComicsAsync(string? queryText, string filter);
        Task<ComicAggregate?> GetComicDetailsAsync(ContentKey ComicKey, bool forceWeb = false);
        Task<IReadOnlyList<ChapterAggregate>> GetAllChaptersAsync(ContentKey ComicKey, string language, bool forceWeb = false);
        Task<IReadOnlyList<ChapterPageModel>> GetChapterPagesAsync(ContentKey chapterKey, bool forceWeb = false);
        Task<IReadOnlyList<ComicSourceModel>> GetComicSourcesAsync();

        Task<Result> UpsertFavoriteComicAsync(ContentKey comic);
        Task<Result> UpsertComicUserDataAsync(ContentKey comicKey, ComicUserData comicUserData);
        Task<Result> UpsertChaptersAsync(ContentKey comicKey, string language);
        Task<Result> UpsertChapterUserDataAsync(ContentKey comicKey, ContentKey chapterKey, ChapterUserData chapterUserData);
        Task<Result> UpsertComicSourceAsync(string pluginPath, bool isEnabled = true);

        Task<Result> RemoveFavoriteComicAsync(ContentKey comicKey);
        Task<Result> RemoveComicSourceAsync(string sourceName);
        Task<Result> CleanupUnfavoriteComicsDataAsync();
    }
}
