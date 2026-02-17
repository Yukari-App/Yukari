using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Models;
using Yukari.Models.Data;
using Yukari.Models.DTO;

namespace Yukari.Services.Storage
{
    public interface IDataService
    {
        Task<IReadOnlyList<ComicModel>> GetFavoriteComicsAsync(string? queryText = null);
        Task<ComicModel?> GetComicDetailsAsync(ContentKey ComicKey);
        Task<ComicUserData> GetComicUserDataAsync(ContentKey ComicKey);
        Task<IReadOnlyList<ChapterModel>> GetAllChaptersAsync(ContentKey ComicKey, string language);
        Task<Dictionary<string, ChapterUserData>> GetAllChaptersUserDataMapAsync(ContentKey comicKey);
        Task<ChapterUserData> GetChapterUserDataAsync(ContentKey comicKey, ContentKey chapterKey);
        Task<IReadOnlyList<ChapterPageModel>> GetChapterPagesAsync(ContentKey comicKey, ContentKey chapterKey);
        Task<IReadOnlyList<ComicSourceModel>> GetComicSourcesAsync();
        Task<ComicSourceModel?> GetComicSourceDetailsAsync(string sourceName);

        Task UpsertFavoriteComicAsync(ComicModel comic);
        Task UpsertComicUserDataAsync(ContentKey comicKey, ComicUserData comicUserData);
        Task UpsertChapterAsync(ChapterModel chapter);
        Task UpsertChaptersAsync(ContentKey comicKey, string language, IEnumerable<ChapterModel> chapters);
        Task UpsertChapterUserDataAsync(ContentKey comicKey, ContentKey chapterKey, ChapterUserData chapterUserData);
        Task UpsertChapterPagesAsync(IReadOnlyList<ChapterPageModel> chapterPages);
        Task UpsertChaptersIsReadAsync(ContentKey comicKey, string[] chapterIDs, bool IsRead);
        Task UpsertComicSourceAsync(ComicSourceModel comicSource);

        Task RemoveFavoriteComicAsync(ContentKey comicKey);
        Task RemoveChapterAsync(ContentKey comicKey,ContentKey chapterKey);
        Task RemoveComicSourceAsync(string sourceName);
        Task CleanupUnfavoriteComicsDataAsync();
    }
}
