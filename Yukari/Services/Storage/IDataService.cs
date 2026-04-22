using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Yukari.Models;
using Yukari.Models.Data;
using Yukari.Models.DTO;

namespace Yukari.Services.Storage
{
    public interface IDataService
    {
        Task<IReadOnlyList<ComicModel>> GetFavoriteComicsAsync(
            string? queryText = null,
            CancellationToken ct = default
        );
        Task<ComicModel?> GetComicDetailsAsync(ContentKey comicKey, CancellationToken ct = default);
        Task<ComicUserData> GetComicUserDataAsync(
            ContentKey comicKey,
            CancellationToken ct = default
        );
        Task<ComicReadingProgress> GetComicReadingProgressAsync(
            ContentKey comicKey,
            string language,
            CancellationToken ct = default
        );
        Task<IReadOnlyList<ChapterModel>> GetAllChaptersAsync(
            ContentKey comicKey,
            string language,
            CancellationToken ct = default
        );
        Task<Dictionary<string, ChapterUserData>> GetAllChaptersUserDataMapAsync(
            ContentKey comicKey,
            CancellationToken ct = default
        );
        Task<ChapterUserData> GetChapterUserDataAsync(
            ContentKey comicKey,
            ContentKey chapterKey,
            CancellationToken ct = default
        );
        Task<IReadOnlyList<ChapterPageModel>> GetChapterPagesAsync(
            ContentKey comicKey,
            ContentKey chapterKey,
            CancellationToken ct = default
        );
        Task<IReadOnlyList<ComicSourceModel>> GetComicSourcesAsync(CancellationToken ct = default);
        Task<ComicSourceModel?> GetComicSourceDetailsAsync(
            string sourceName,
            CancellationToken ct = default
        );
        Task<IReadOnlyList<ComicSourceModel>> GetComicSourcesPendingRemovalAsync(
            CancellationToken ct = default
        );
        Task<IReadOnlyList<ComicSourceModel>> GetComicSourcesPendingUpdateAsync(
            CancellationToken ct = default
        );

        Task UpsertFavoriteComicAsync(ComicModel comic);
        Task UpsertComicUserDataAsync(ContentKey comicKey, ComicUserData comicUserData);
        Task UpsertComicReadingProgressAsync(ContentKey comicKey, ComicReadingProgress progress);
        Task UpsertChapterAsync(ChapterModel chapter);
        Task UpsertChaptersAsync(
            ContentKey comicKey,
            string language,
            IEnumerable<ChapterModel> chapters
        );
        Task UpsertChapterUserDataAsync(
            ContentKey comicKey,
            ContentKey chapterKey,
            ChapterUserData chapterUserData
        );
        Task UpsertChapterPagesAsync(IReadOnlyList<ChapterPageModel> chapterPages);
        Task UpsertChaptersIsReadAsync(ContentKey comicKey, string[] chapterIDs, bool IsRead);
        Task UpsertComicSourceAsync(ComicSourceModel comicSource);
        Task UpdateComicSourceIsEnabledAsync(string sourceName, bool isEnabled);
        Task UpdateComicSourcePendingRemovalAsync(string sourceName, bool pendingRemoval);
        Task UpdateComicSourcePendingUpdateAsync(string sourceName, string? pendingUpdatePath);

        Task RemoveFavoriteComicAsync(ContentKey comicKey);
        Task RemoveChapterAsync(ContentKey comicKey, ContentKey chapterKey);
        Task RemoveComicSourceAsync(string sourceName);

        Task<IReadOnlyList<ContentKey>> CleanupUnfavoriteComicsDataAsync();
    }
}
