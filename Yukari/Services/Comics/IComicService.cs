using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Core.Models;
using Yukari.Models;
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

        Task<bool> UpsertFavoriteComicAsync(ComicModel comic, string selectedLanguage);
        Task<bool> UpsertComicUserDataAsync(ContentKey comicKey, ComicUserData comicUserData);
        Task<bool> UpsertChapterUserDataAsync(ContentKey comicKey, ContentKey chapterKey, ChapterUserData chapterUserData);
        Task<bool> UpsertComicSourceAsync(ComicSourceModel comicSource);

        Task<bool> RemoveFavoriteComicAsync(ContentKey comicKey);
        Task<bool> RemoveComicSourceAsync(string sourceName);
        Task<bool> CleanupUnfavoriteComicsDataAsync();
    }
}
