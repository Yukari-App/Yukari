using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Yukari.Core.Models;
using Yukari.Models;
using Yukari.Models.Common;
using Yukari.Models.Data;
using Yukari.Models.DTO;

namespace Yukari.Services.Comics;

public interface IComicService
{
    Task<Result<IReadOnlyList<Filter>>> GetSourceFiltersAsync(
        string sourceName,
        CancellationToken ct = default
    );
    Task<Result<IReadOnlyDictionary<string, string>>> GetSourceLanguagesAsync(
        string sourceName,
        CancellationToken ct = default
    );

    Task<Result<IReadOnlyList<ComicModel>>> SearchComicsAsync(
        string sourceName,
        string? queryText,
        IReadOnlyDictionary<string, IReadOnlyList<string>> filters,
        CancellationToken ct = default
    );
    Task<Result<IReadOnlyList<ComicModel>>> GetFavoriteComicsAsync(
        string? queryText,
        string filter,
        CancellationToken ct = default
    );
    Task<Result<ComicAggregate?>> GetComicDetailsAsync(
        ContentKey ComicKey,
        bool forceWeb = false,
        CancellationToken ct = default
    );
    Task<Result<ComicReadingProgress>> GetComicReadingProgressAsync(
        ContentKey comicKey,
        string language,
        CancellationToken ct = default
    );
    Task<Result<IReadOnlyList<ChapterAggregate>>> GetAllChaptersAsync(
        ContentKey ComicKey,
        string language,
        bool forceWeb = false,
        CancellationToken ct = default
    );
    Task<Result<ChapterUserData>> GetChapterUserDataAsync(
        ContentKey comicKey,
        ContentKey chapterKey,
        CancellationToken ct = default
    );
    Task<Result<IReadOnlyList<ChapterPageModel>>> GetChapterPagesAsync(
        ContentKey comicKey,
        ContentKey chapterKey,
        bool forceWeb = false,
        CancellationToken ct = default
    );
    Task<Result<IReadOnlyList<ComicSourceModel>>> GetComicSourcesAsync(
        CancellationToken ct = default
    );

    Task<Result> UpsertFavoriteComicAsync(ContentKey comic);
    Task<Result> UpsertComicUserDataAsync(ContentKey comicKey, ComicUserData comicUserData);
    Task<Result> UpsertComicReadingProgressAsync(
        ContentKey comicKey,
        ComicReadingProgress progress
    );
    Task<Result> UpsertChaptersAsync(ContentKey comicKey, string language);
    Task<Result> UpsertChapterUserDataAsync(
        ContentKey comicKey,
        ContentKey chapterKey,
        ChapterUserData chapterUserData
    );
    Task<Result> UpsertChaptersIsReadAsync(ContentKey comicKey, string[] chapterIDs, bool IsRead);
    Task<Result> UpsertComicSourceAsync(string pluginPath);
    Task<Result> UpdateComicSourceIsEnabledAsync(string sourceName, bool isEnabled);

    Task<Result> RemoveFavoriteComicAsync(ContentKey comicKey);
    Task<Result> RemoveComicSourceAsync(string sourceName);
    Task<Result> CleanupUnfavoriteComicsDataAsync();
}
