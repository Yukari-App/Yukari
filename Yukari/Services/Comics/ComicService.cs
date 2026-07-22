using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Yukari.Core.Models;
using Yukari.Enums;
using Yukari.Exceptions;
using Yukari.Helpers;
using Yukari.Models;
using Yukari.Models.Common;
using Yukari.Models.Data;
using Yukari.Models.DTO;
using Yukari.Services.Sources;
using Yukari.Services.Storage;
using Yukari.Services.UI;

namespace Yukari.Services.Comics;

internal class ComicService : IComicService
{
    private readonly IDataService _dbService;
    private readonly ISourceService _srcService;
    private readonly ILocalSourceService _localService;
    private readonly IDownloadService _dloadService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<ComicService> _logger;

    public ComicService(
        IDataService dbService,
        ISourceService srcService,
        ILocalSourceService localService,
        IDownloadService dloadService,
        ILocalizationService localizationService,
        ILogger<ComicService> logger
    )
    {
        _dbService = dbService;
        _srcService = srcService;
        _localService = localService;
        _dloadService = dloadService;
        _localizationService = localizationService;
        _logger = logger;
    }

    #region Read Methods

    public async Task<Result<IReadOnlyList<Filter>>> GetSourceFiltersAsync(
        string sourceName,
        CancellationToken ct = default
    )
    {
        return await ExecuteAsync(
            async (ct) =>
                Result<IReadOnlyList<Filter>>.Success(
                    await _srcService.GetFiltersAsync(sourceName, ct)
                ),
            _localizationService.GetString("ErrorGettingSourceFilters"),
            ct
        );
    }

    public async Task<Result<IReadOnlyDictionary<string, string>>> GetSourceLanguagesAsync(
        string sourceName,
        CancellationToken ct = default
    )
    {
        return await ExecuteAsync(
            async (ct) =>
                Result<IReadOnlyDictionary<string, string>>.Success(
                    await _srcService.GetLanguagesAsync(sourceName, ct)
                ),
            _localizationService.GetString("ErrorGettingSourceLanguages"),
            ct
        );
    }

    public async Task<Result<IReadOnlyList<ComicModel>>> SearchComicsAsync(
        string sourceName,
        string? queryText,
        IReadOnlyDictionary<string, IReadOnlyList<string>> filters,
        int page = 1,
        CancellationToken ct = default
    )
    {
        return await ExecuteAsync(
            async (ct) =>
            {
                _logger.LogDebug(
                    "Searching {Source} for '{Query}' with filters {@Filters}",
                    sourceName,
                    queryText,
                    filters
                );

                var comics = string.IsNullOrEmpty(queryText)
                    ? await _srcService.GetTrendingComicsAsync(sourceName, filters, page, ct)
                    : await _srcService.SearchComicsAsync(sourceName, queryText, filters, page, ct);

                return Result<IReadOnlyList<ComicModel>>.Success(comics);
            },
            _localizationService.GetString("ErrorFetchingComics"),
            ct
        );
    }

    public async Task<Result<IReadOnlyList<ComicModel>>> GetFavoriteComicsAsync(
        string? queryText,
        string? collectionName,
        FavoritesSortBy sortBy,
        SortDirection sortDirection,
        CancellationToken ct = default
    )
    {
        return await ExecuteAsync(
            async (ct) =>
                Result<IReadOnlyList<ComicModel>>.Success(
                    await _dbService.GetFavoriteComicsAsync(
                        queryText,
                        collectionName,
                        sortBy == FavoritesSortBy.LastRead ? "lastread" : "title",
                        sortDirection == SortDirection.Descending,
                        ct
                    )
                ),
            _localizationService.GetString("ErrorFetchingFavoriteComics"),
            ct
        );
    }

    public async Task<Result<ComicAggregate?>> GetComicDetailsAsync(
        ContentKey comicKey,
        bool forceWeb = false,
        CancellationToken ct = default
    )
    {
        return await ExecuteAsync(
            async (ct) =>
            {
                var userData = await _dbService.GetComicUserDataAsync(comicKey, ct);
                ComicModel? comic;

                // Cache-first: reads from DB for favorites to avoid unnecessary network calls.
                // forceWeb bypasses the cache when the user explicitly requests a refresh.
                if (userData.IsFavorite && !forceWeb)
                {
                    comic = await _dbService.GetComicDetailsAsync(comicKey, ct);
                }
                else
                {
                    comic = await _srcService.GetComicDetailsAsync(
                        comicKey.Source,
                        comicKey.Id,
                        ct
                    );
                }

                if (comic == null)
                    return Result<ComicAggregate?>.Success(null);

                return Result<ComicAggregate?>.Success(new ComicAggregate(comic, userData));
            },
            _localizationService.GetString("ErrorFetchingComicDetails"),
            ct
        );
    }

    public async Task<Result<ComicUserData>> GetComicUserDataAsync(
        ContentKey comicKey,
        CancellationToken ct = default
    )
    {
        return await ExecuteAsync(
            async (ct) =>
                Result<ComicUserData>.Success(await _dbService.GetComicUserDataAsync(comicKey, ct)),
            _localizationService.GetString("ErrorFetchingComicUserData"),
            ct
        );
    }

    public async Task<Result<IReadOnlyList<string>>> GetCollectionsAsync(
        CancellationToken ct = default
    )
    {
        return await ExecuteAsync(
            async (ct) =>
                Result<IReadOnlyList<string>>.Success(await _dbService.GetCollectionsAsync(ct)),
            _localizationService.GetString("ErrorFetchingCollections"),
            ct
        );
    }

    public async Task<Result<ComicReadingProgress>> GetComicReadingProgressAsync(
        ContentKey comicKey,
        string language,
        CancellationToken ct = default
    )
    {
        return await ExecuteAsync(
            async (ct) =>
                Result<ComicReadingProgress>.Success(
                    await _dbService.GetComicReadingProgressAsync(comicKey, language, ct)
                ),
            _localizationService.GetString("ErrorFetchingComicReadingProgress"),
            ct
        );
    }

    public async Task<Result<IReadOnlyList<ChapterAggregate>>> GetAllChaptersAsync(
        ContentKey comicKey,
        string language,
        bool forceWeb = false,
        CancellationToken ct = default
    )
    {
        return await ExecuteAsync(
            async (ct) =>
            {
                var userDataMap = await _dbService.GetAllChaptersUserDataMapAsync(comicKey, ct);
                IReadOnlyList<ChapterModel> chapters;
                var comicUserData = await _dbService.GetComicUserDataAsync(comicKey, ct);

                if (comicUserData.IsFavorite && !forceWeb)
                {
                    chapters = await _dbService.GetAllChaptersAsync(comicKey, language, ct);
                    if (chapters.Count == 0 && !comicKey.IsLocal())
                    {
                        _logger.LogDebug(
                            "No chapters in cache from language '{Language}' for comic {ComicKey}, fetching from web",
                            language,
                            comicKey
                        );
                        chapters = await FetchAndCacheChaptersAsync(comicKey, language, ct: ct);
                    }
                }
                else
                {
                    chapters = await FetchAndCacheChaptersAsync(
                        comicKey,
                        language,
                        comicUserData.IsFavorite,
                        ct
                    );
                }

                return Result<IReadOnlyList<ChapterAggregate>>.Success(
                    chapters
                        .Select(c =>
                        {
                            var userData =
                                userDataMap.GetValueOrDefault(c.Id) ?? new ChapterUserData();
                            return new ChapterAggregate(c, userData);
                        })
                        .ToList()
                );
            },
            _localizationService.GetString("ErrorFetchingChapters"),
            ct
        );
    }

    public async Task<Result<ChapterUserData>> GetChapterUserDataAsync(
        ContentKey comicKey,
        ContentKey chapterKey,
        CancellationToken ct = default
    )
    {
        return await ExecuteAsync(
            async (ct) =>
                Result<ChapterUserData>.Success(
                    await _dbService.GetChapterUserDataAsync(comicKey, chapterKey, ct)
                ),
            _localizationService.GetString("ErrorFetchingChapterUserData"),
            ct
        );
    }

    public async Task<Result<IReadOnlyList<ChapterPageModel>>> GetChapterPagesAsync(
        ContentKey comicKey,
        ContentKey chapterKey,
        bool forceWeb = false,
        CancellationToken ct = default
    )
    {
        return await ExecuteAsync(
            async (ct) =>
            {
                var chapterUserData = await _dbService.GetChapterUserDataAsync(
                    comicKey,
                    chapterKey,
                    ct
                );
                IReadOnlyList<ChapterPageModel> pages;

                if ((chapterUserData.IsDownloaded && !forceWeb) || comicKey.IsLocal())
                {
                    pages = await _dbService.GetChapterPagesAsync(comicKey, chapterKey, ct);
                    if (pages.Count == 0 && !comicKey.IsLocal())
                        throw new InvalidOperationException(
                            "Chapter marked as downloaded, but no pages found locally. Try downloading again."
                        );
                }
                else
                {
                    pages = await _srcService.GetChapterPagesAsync(
                        comicKey.Source,
                        comicKey.Id,
                        chapterKey.Id,
                        ct
                    );
                }

                return Result<IReadOnlyList<ChapterPageModel>>.Success(pages);
            },
            _localizationService.GetString("ErrorFetchingChapterPages"),
            ct
        );
    }

    public async Task<Result<IReadOnlyList<ComicSourceModel>>> GetComicSourcesAsync(
        CancellationToken ct = default
    )
    {
        return await ExecuteAsync(
            async (ct) =>
                Result<IReadOnlyList<ComicSourceModel>>.Success(
                    await _dbService.GetComicSourcesAsync(ct)
                ),
            _localizationService.GetString("ErrorFetchingComicSources"),
            ct
        );
    }

    #endregion

    #region Write Methods

    public async Task<Result> UpsertFavoriteComicAsync(ContentKey comicKey)
    {
        return await ExecuteAsync(
            async () =>
            {
                var comicDetails = await _srcService.GetComicDetailsAsync(
                    comicKey.Source,
                    comicKey.Id
                );

                if (comicDetails == null)
                {
                    var comicUserData = await _dbService.GetComicUserDataAsync(comicKey);
                    if (!comicUserData.IsFavorite)
                        return Result.Failure(
                            "The comic doesn't exist in the source and cannot be favorited."
                        );

                    comicDetails =
                        await _dbService.GetComicDetailsAsync(comicKey)
                        ?? throw new InvalidOperationException(
                            $"Comic '{comicKey.Id}' is marked as favorite but was not found in the database. Data may be corrupted."
                        );

                    comicDetails.IsAvailable = false;
                }

                var localCover = await _dloadService.DownloadComicCoverAsync(
                    comicDetails.CoverImageUrl,
                    comicKey
                );
                if (!string.IsNullOrWhiteSpace(localCover))
                    comicDetails.CoverImageUrl = localCover;

                await _dbService.UpsertFavoriteComicAsync(comicDetails);

                _logger.LogInformation("Comic {ComicKey} added to favorites", comicKey);
                return Result.Success();
            },
            _localizationService.GetString("FailedAddingToFavorites")
        );
    }

    public async Task<Result<ContentKey>> UpsertLocalComicAsync(
        LocalComicInfo localComicInfo,
        string? comicId = null
    )
    {
        return await ExecuteAsync(
            async (ct) =>
            {
                var comic = new ComicModel()
                {
                    Id = comicId ?? Guid.NewGuid().ToString(),
                    Source = LocalComicConstants.SourceName,
                    // ComicUrl is reused for local comics, storing the chapter path encoded
                    // along with the format—this avoids the need for migration.
                    // See LocalComicConstants.
                    ComicUrl = LocalComicConstants.EncodeChaptersPath(
                        localComicInfo.ChaptersPath,
                        localComicInfo.ChaptersFormat
                    ),
                    Title = localComicInfo.Title,
                    Author = localComicInfo.Author,
                    Description = localComicInfo.Description,
                    Tags = localComicInfo.Tags,
                    Year = localComicInfo.Year,
                    CoverImageUrl = localComicInfo.CoverImageUrl,
                    Langs = new[] { new LanguageModel(LocalComicConstants.SourceName, "Local") },
                };

                await _dbService.UpsertFavoriteComicAsync(comic);

                _logger.LogInformation(
                    "Comic {ComicKey} added to favorites",
                    new ContentKey(comic.Id, comic.Source)
                );
                return Result<ContentKey>.Success(new(comic.Id, comic.Source));
            },
            _localizationService.GetString("FailedToAddLocalComic")
        );
    }

    public async Task<Result> UpsertComicUserDataAsync(
        ContentKey comicKey,
        ComicUserData comicUserData
    )
    {
        return await ExecuteAsync(
            async () =>
            {
                await _dbService.UpsertComicUserDataAsync(comicKey, comicUserData);

                _logger.LogDebug("User data updated for comic {ComicKey}", comicKey);
                return Result.Success();
            },
            _localizationService.GetString("ErrorSavingProgress")
        );
    }

    public async Task<Result> CreateCollectionAsync(string name)
    {
        return await ExecuteAsync(
            async () =>
            {
                await _dbService.InsertCollectionAsync(name);

                _logger.LogInformation("Collection '{Name}' created", name);
                return Result.Success();
            },
            _localizationService.GetString("ErrorCreatingCollection")
        );
    }

    public async Task<Result> RenameCollectionAsync(string oldName, string newName)
    {
        return await ExecuteAsync(
            async () =>
            {
                await _dbService.RenameCollectionAsync(oldName, newName);

                _logger.LogInformation(
                    "Collection '{OldName}' renamed to '{NewName}'",
                    oldName,
                    newName
                );
                return Result.Success();
            },
            _localizationService.GetString("ErrorRenamingCollection")
        );
    }

    public async Task<Result> AddComicToCollectionAsync(ContentKey comicKey, string collectionName)
    {
        return await ExecuteAsync(
            async () =>
            {
                await _dbService.AddComicToCollectionAsync(comicKey, collectionName);

                _logger.LogInformation(
                    "Comic {ComicKey} added to collection '{CollectionName}'",
                    comicKey,
                    collectionName
                );
                return Result.Success();
            },
            _localizationService.GetString("ErrorAddingComicToCollection")
        );
    }

    public async Task<Result> UpsertComicReadingProgressAsync(
        ContentKey comicKey,
        ComicReadingProgress progress
    )
    {
        return await ExecuteAsync(
            async () =>
            {
                await _dbService.UpsertComicReadingProgressAsync(comicKey, progress);

                _logger.LogDebug(
                    "Reading progress saved for comic {ComicKey} - Language: {Language}, LastChapterId: {LastChapterId}",
                    comicKey,
                    progress.LanguageCode,
                    progress.LastChapterId
                );
                return Result.Success();
            },
            _localizationService.GetString("ErrorSavingComicReadingProgress")
        );
    }

    public async Task<Result> UpsertChaptersAsync(ContentKey comicKey, string language)
    {
        return await ExecuteAsync(
            async () =>
            {
                await FetchAndCacheChaptersAsync(comicKey, language);

                _logger.LogInformation(
                    "Chapters updated for comic {ComicKey} - Language: {Language}",
                    comicKey,
                    language
                );
                return Result.Success();
            },
            _localizationService.GetString("ErrorPersistingChapters")
        );
    }

    public async Task<Result> UpsertLocalChaptersAsync(
        ContentKey comicKey,
        string chaptersPath,
        LocalChaptersFormat chaptersFormat
    )
    {
        return await ExecuteAsync(
            async () =>
            {
                if (!comicKey.IsLocal())
                    throw new InvalidOperationException($"Comic {comicKey} isn't a Local Comic");

                var chapters = await _localService.ScanChaptersAsync(chaptersPath, chaptersFormat);

                await _dbService.UpsertChaptersAsync(
                    comicKey,
                    LocalComicConstants.SourceName,
                    chapters,
                    false
                );

                foreach (var chapter in chapters)
                {
                    if (chapter.LocalPath == null)
                        continue;

                    var pages = await _localService.GetPagesAsync(
                        chapter.LocalPath,
                        chaptersFormat
                    );
                    if (pages.Count > 0)
                    {
                        await _dbService.UpsertChapterPagesAsync(
                            comicKey,
                            new ContentKey(chapter.Id, chapter.Source),
                            pages
                        );
                    }
                }

                _logger.LogInformation("Chapters updated for comic {ComicKey}", comicKey);
                return Result.Success();
            },
            _localizationService.GetString("ErrorPersistingLocalChapters")
        );
    }

    public async Task<Result> RescanLocalChaptersAsync(
        ContentKey comicKey,
        string encodedChaptersPath
    )
    {
        return await ExecuteAsync(
            async () =>
            {
                var (path, format) = LocalComicConstants.DecodeChaptersPath(encodedChaptersPath);
                return await UpsertLocalChaptersAsync(comicKey, path, format);
            },
            _localizationService.GetString("ErrorRescanningLocalChapters")
        );
    }

    public async Task<Result> UpsertChapterUserDataAsync(
        ContentKey comicKey,
        ContentKey chapterKey,
        ChapterUserData chapterUserData
    )
    {
        return await ExecuteAsync(
            async () =>
            {
                await _dbService.UpsertChapterUserDataAsync(comicKey, chapterKey, chapterUserData);

                _logger.LogDebug(
                    "User data saved for chapter {ChapterKey} of comic {ComicKey}",
                    chapterKey,
                    comicKey
                );
                return Result.Success();
            },
            _localizationService.GetString("ErrorSavingChapterProgress")
        );
    }

    public async Task<Result> UpsertChaptersIsReadAsync(
        ContentKey comicKey,
        string[] chapterIDs,
        bool isRead
    )
    {
        return await ExecuteAsync(
            async () =>
            {
                await _dbService.UpsertChaptersIsReadAsync(comicKey, chapterIDs, isRead);

                _logger.LogInformation(
                    "{Count} chapters marked as {Status} for comic {ComicKey}",
                    chapterIDs.Length,
                    isRead ? "read" : "unread",
                    comicKey
                );
                return Result.Success();
            },
            _localizationService.GetString("ErrorSettingReadStatus")
        );
    }

    public async Task<Result> UpdateChapterPageCountAsync(
        ContentKey comicKey,
        ContentKey chapterKey,
        int? count
    )
    {
        return await ExecuteAsync(
            async () =>
            {
                await _dbService.UpdateChapterPageCountAsync(comicKey, chapterKey, count);
                return Result.Success();
            },
            _localizationService.GetString("ErrorUpdatingChapterPageCount")
        );
    }

    public async Task<Result> UpsertComicSourceAsync(string pluginPath)
    {
        return await ExecuteAsync(
            async () =>
            {
                var comicSource = await _srcService.GetComicSourceModelFromAssemblyAsync(
                    pluginPath,
                    true
                );
                if (
                    comicSource.Name.Equals(
                        LocalComicConstants.SourceName,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                    return Result.Failure("You can't add a ComicSource named 'Local'");

                try
                {
                    comicSource.DllPath = AppDataHelper.CopyDllToPluginsDirectory(pluginPath);
                    comicSource.LogoUrl = await _dloadService.DownloadPluginLogoAsync(
                        comicSource.LogoUrl,
                        comicSource.Name
                    );

                    await _dbService.UpsertComicSourceAsync(comicSource);

                    _logger.LogInformation(
                        "Comic source '{SourceName}' (version {Version}) has been added/updated",
                        comicSource.Name,
                        comicSource.Version
                    );
                    return Result.Success();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(
                        ex,
                        "Could not replace DLL for source '{SourceName}' because it's in use. Marking for pending update on next restart.",
                        comicSource.Name
                    );
                    await _dbService.UpdateComicSourcePendingUpdateAsync(
                        comicSource.Name,
                        pluginPath
                    );
                    return Result.PendingRestart();
                }
            },
            _localizationService.GetString("ErrorAddingUpdatingComicSource")
        );
    }

    public async Task<Result> UpdateComicSourceIsEnabledAsync(string sourceName, bool isEnabled)
    {
        return await ExecuteAsync(
            async () =>
            {
                await _dbService.UpdateComicSourceIsEnabledAsync(sourceName, isEnabled);

                _logger.LogInformation(
                    "Comic source '{SourceName}' has been {Status}",
                    sourceName,
                    isEnabled ? "enabled" : "disabled"
                );
                return Result.Success();
            },
            _localizationService.GetString("ErrorUpdatingComicSourceStatus")
        );
    }

    #endregion

    #region Delete Methods

    public async Task<Result> RemoveFavoriteComicAsync(ContentKey comicKey)
    {
        return await ExecuteAsync(
            async () =>
            {
                await _dbService.RemoveFavoriteComicAsync(comicKey);
                await _dloadService.DeleteComicDataAndDownloadsAsync(comicKey);

                _logger.LogInformation("Comic {ComicKey} removed from favorites", comicKey);
                return Result.Success();
            },
            _localizationService.GetString("ErrorRemovingComicFromFavorites")
        );
    }

    public async Task<Result> RemoveCollectionAsync(string collectionName)
    {
        return await ExecuteAsync(
            async () =>
            {
                await _dbService.RemoveCollectionAsync(collectionName);

                _logger.LogInformation("Collection '{Name}' removed", collectionName);
                return Result.Success();
            },
            _localizationService.GetString("ErrorRemovingCollection")
        );
    }

    public async Task<Result> RemoveComicFromCollectionAsync(
        ContentKey comicKey,
        string collectionName
    )
    {
        return await ExecuteAsync(
            async () =>
            {
                await _dbService.RemoveComicFromCollectionAsync(comicKey, collectionName);

                _logger.LogInformation(
                    "Comic {ComicKey} removed from collection '{CollectionName}'",
                    comicKey,
                    collectionName
                );
                return Result.Success();
            },
            _localizationService.GetString("ErrorRemovingComicFromCollection")
        );
    }

    public async Task<Result> RemoveComicSourceAsync(string sourceName)
    {
        return await ExecuteAsync(
            async () =>
            {
                try
                {
                    var comicSource =
                        await _dbService.GetComicSourceDetailsAsync(sourceName)
                        ?? throw new InvalidOperationException(
                            $"The source '{sourceName}' is not registered in the database."
                        );

                    if (
                        !string.IsNullOrWhiteSpace(comicSource.DllPath)
                        && File.Exists(comicSource.DllPath)
                    )
                        File.Delete(comicSource.DllPath);

                    await _dbService.RemoveComicSourceAsync(sourceName);

                    _logger.LogInformation("Comic source '{SourceName}' removed", sourceName);
                    return Result.Success();
                }
                // DLL files loaded by AssemblyLoadContext.Default cannot be deleted while the process is running.
                // Removal and updates are deferred to the next startup via PendingRemoval/PendingUpdatePath,
                // when no plugin assembly has been loaded yet.
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(
                        ex,
                        "Could not delete DLL for source '{SourceName}' because it's in use. Marking for pending removal on next restart.",
                        sourceName
                    );
                    await _dbService.UpdateComicSourcePendingRemovalAsync(sourceName, true);
                    return Result.PendingRestart();
                }
            },
            _localizationService.GetString("ErrorRemovingComicSource")
        );
    }

    public async Task<Result> CleanupUnfavoriteComicsDataAsync()
    {
        return await ExecuteAsync(
            async () =>
            {
                var unfavoriteComics = await _dbService.CleanupUnfavoriteComicsDataAsync();
                await _dloadService.CleanupUnfavoriteComicsAsync(unfavoriteComics);

                _logger.LogInformation("Storage cleanup completed");
                return Result.Success();
            },
            _localizationService.GetString("ErrorCleaningUpData")
        );
    }

    #endregion

    #region Helpers

    private async Task<IReadOnlyList<ChapterModel>> FetchAndCacheChaptersAsync(
        ContentKey comicKey,
        string language,
        bool shouldSave = true,
        CancellationToken ct = default
    )
    {
        var chapters = await _srcService.GetAllChaptersAsync(
            comicKey.Source,
            comicKey.Id,
            language,
            ct
        );

        if (shouldSave)
            await _dbService.UpsertChaptersAsync(comicKey, language, chapters);

        return chapters;
    }

    // Wraps all public operations: catches OperationCanceledException, ComicSourceDisabledException,
    // network errors, and generic exceptions — converting them into Result<T>.
    // No public method in this service needs its own try/catch.
    private async Task<Result<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<Result<T>>> action,
        string errorTitle,
        CancellationToken ct = default
    )
    {
        try
        {
            return await action(ct);
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Cancelled();
        }
        catch (ComicSourceDisabledException ex)
        {
            return Result<T>.ComicSourceDisabled(ex.Message, errorTitle);
        }
        catch (Exception ex) when (IsNetworkError(ex))
        {
            _logger.LogWarning(ex, "Network error in {Operation}", errorTitle);
            return Result<T>.Failure(
                _localizationService.GetString("NoInternetConnectionOrSourceIsOffline"),
                errorTitle
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in ComicService — {ErrorTitle}", errorTitle);
            var message = ex.InnerException?.Message ?? ex.Message;
            return Result<T>.Failure(message, errorTitle);
        }
    }

    private async Task<Result> ExecuteAsync(Func<Task<Result>> action, string errorTitle)
    {
        try
        {
            return await action();
        }
        catch (ComicSourceDisabledException ex)
        {
            return Result.ComicSourceDisabled(ex.Message, errorTitle);
        }
        catch (Exception ex) when (IsNetworkError(ex))
        {
            _logger.LogWarning(ex, "Network error in {Operation}", errorTitle);
            return Result.Failure(
                _localizationService.GetString("NoInternetConnectionOrSourceIsOffline"),
                errorTitle
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in ComicService — {ErrorTitle}", errorTitle);
            var message = ex.InnerException?.Message ?? ex.Message;
            return Result.Failure(message, errorTitle);
        }
    }

    private bool IsNetworkError(Exception ex)
    {
        return ex is System.Net.Http.HttpRequestException
            || ex is System.Net.Sockets.SocketException
            || ex.InnerException is System.Net.Http.HttpRequestException;
    }

    #endregion
}
