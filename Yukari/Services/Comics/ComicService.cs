using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Yukari.Core.Models;
using Yukari.Exceptions;
using Yukari.Helpers;
using Yukari.Models;
using Yukari.Models.Common;
using Yukari.Models.Data;
using Yukari.Models.DTO;
using Yukari.Services.Sources;
using Yukari.Services.Storage;

namespace Yukari.Services.Comics;

internal class ComicService : IComicService
{
    private readonly IDataService _dbService;
    private readonly ISourceService _srcService;
    private readonly IDownloadService _dloadService;
    private readonly ILogger<ComicService> _logger;

    public ComicService(
        IDataService dbService,
        ISourceService srcService,
        IDownloadService dloadService,
        ILogger<ComicService> logger
    )
    {
        _dbService = dbService;
        _srcService = srcService;
        _dloadService = dloadService;
        _logger = logger;
    }

    // --- Read Methods ---

    public async Task<Result<IReadOnlyList<Filter>>> GetSourceFiltersAsync(
        string sourceName,
        CancellationToken ct = default
    )
    {
        return await ExecuteAsync(
            async (ct) =>
            {
                await LoadComicSourceAsync(sourceName, ct);
                return Result<IReadOnlyList<Filter>>.Success(_srcService.GetFilters());
            },
            "Error getting source filters",
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
            {
                await LoadComicSourceAsync(sourceName, ct);
                return Result<IReadOnlyDictionary<string, string>>.Success(
                    _srcService.GetLanguages()
                );
            },
            "Error getting source languages",
            ct
        );
    }

    public async Task<Result<IReadOnlyList<ComicModel>>> SearchComicsAsync(
        string sourceName,
        string? queryText,
        IReadOnlyDictionary<string, IReadOnlyList<string>> filters,
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

                await LoadComicSourceAsync(sourceName, ct);
                var comics = string.IsNullOrEmpty(queryText)
                    ? await _srcService.GetTrendingComicsAsync(filters, ct)
                    : await _srcService.SearchComicsAsync(queryText, filters, ct);

                return Result<IReadOnlyList<ComicModel>>.Success(comics);
            },
            "Error fetching comics",
            ct
        );
    }

    public async Task<Result<IReadOnlyList<ComicModel>>> GetFavoriteComicsAsync(
        string? queryText,
        string? collectionName,
        CancellationToken ct = default
    )
    {
        return await ExecuteAsync(
            async (ct) =>
                Result<IReadOnlyList<ComicModel>>.Success(
                    await _dbService.GetFavoriteComicsAsync(queryText, collectionName, ct)
                ),
            "Error fetching favorite comics",
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
                    await LoadComicSourceAsync(comicKey.Source, ct);
                    comic = await _srcService.GetComicDetailsAsync(comicKey.Id, ct);
                }

                if (comic == null)
                    return Result<ComicAggregate?>.Success(null);

                return Result<ComicAggregate?>.Success(new ComicAggregate(comic, userData));
            },
            "Error fetching comic details",
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
            "Error fetching collections",
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
            "Error fetching comic reading progress",
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
                    if (chapters.Count == 0)
                    {
                        _logger.LogWarning(
                            "No chapters in cache for {ComicKey} from {Source}, fetching from web",
                            comicKey,
                            comicKey.Source
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
            "Error fetching chapters",
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
            "Error fetching chapter user data",
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

                if (chapterUserData.IsDownloaded && !forceWeb)
                {
                    pages = await _dbService.GetChapterPagesAsync(comicKey, chapterKey, ct);
                    if (pages.Count == 0)
                        throw new InvalidOperationException(
                            "Chapter marked as downloaded, but no pages found locally. Try downloading again."
                        );
                }
                else
                {
                    await LoadComicSourceAsync(comicKey.Source, ct);
                    pages = await _srcService.GetChapterPagesAsync(comicKey.Id, chapterKey.Id, ct);
                }

                return Result<IReadOnlyList<ChapterPageModel>>.Success(pages);
            },
            "Error fetching chapter pages",
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
            "Error fetching comic sources",
            ct
        );
    }

    // --- Write Methods ---

    public async Task<Result> UpsertFavoriteComicAsync(ContentKey comicKey)
    {
        return await ExecuteAsync(
            async () =>
            {
                await LoadComicSourceAsync(comicKey.Source);
                var comicDetails = await _srcService.GetComicDetailsAsync(comicKey.Id);

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

                _logger.LogInformation(
                    "Comic '{Id}' from '{Source}' added to favorites",
                    comicKey.Id,
                    comicKey.Source
                );
                return Result.Success();
            },
            "Failed to add to favorites"
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

                _logger.LogDebug(
                    "User data updated for comic '{Id}' from '{Source}'",
                    comicKey.Id,
                    comicKey.Source
                );
                return Result.Success();
            },
            "Error saving progress"
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
            "Error creating collection"
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
            "Error renaming collection"
        );
    }

    public async Task<Result> AddComicToCollectionAsync(ContentKey comicKey, string collectionName)
    {
        return await ExecuteAsync(
            async () =>
            {
                await _dbService.AddComicToCollectionAsync(comicKey, collectionName);

                _logger.LogInformation(
                    "Comic '{Id}' from '{Source}' added to collection '{CollectionName}'",
                    comicKey.Id,
                    comicKey.Source,
                    collectionName
                );
                return Result.Success();
            },
            "Error adding comic to collection"
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
                    "Reading progress saved for comic '{Id}' from '{Source}' - Language: {Language}, LastChapterId: {LastChapterId}",
                    comicKey.Id,
                    comicKey.Source,
                    progress.LanguageCode,
                    progress.LastChapterId
                );
                return Result.Success();
            },
            "Error saving comic reading progress"
        );
    }

    public async Task<Result> UpsertChaptersAsync(ContentKey comicKey, string language)
    {
        return await ExecuteAsync(
            async () =>
            {
                await FetchAndCacheChaptersAsync(comicKey, language);

                _logger.LogInformation(
                    "Chapters updated for comic '{Id}' from '{Source}' - Language: {Language}'",
                    comicKey.Id,
                    comicKey.Source,
                    language
                );
                return Result.Success();
            },
            "Error persisting chapters"
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
                    "User data saved for chapter '{ChapterId}' of comic '{ComicId}' from '{Source}'",
                    chapterKey.Id,
                    comicKey.Id,
                    comicKey.Source
                );
                return Result.Success();
            },
            "Error saving chapter progress"
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
                    "{Count} chapters marked as {Status} for comic '{Id}'",
                    chapterIDs.Length,
                    isRead ? "read" : "unread",
                    comicKey.Id
                );
                return Result.Success();
            },
            "Error setting read status"
        );
    }

    public async Task<Result> UpsertComicSourceAsync(string pluginPath)
    {
        return await ExecuteAsync(
            async () =>
            {
                try
                {
                    var newPath = AppDataHelper.CopyDllToPluginsDirectory(pluginPath);
                    var comicSource = _srcService.GetComicSourceModelFromAssembly(newPath);
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
                    var metadata = _srcService.GetComicSourceModelFromAssembly(pluginPath);

                    _logger.LogWarning(
                        ex,
                        "Could not replace DLL for source '{SourceName}' because it's in use. Marking for pending update on next restart.",
                        metadata.Name
                    );
                    await _dbService.UpdateComicSourcePendingUpdateAsync(metadata.Name, pluginPath);
                    return Result.PendingRestart();
                }
            },
            "Error saving comic source"
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
            "Error updating comic source status"
        );
    }

    public async Task<Result> RemoveFavoriteComicAsync(ContentKey comicKey)
    {
        return await ExecuteAsync(
            async () =>
            {
                await _dbService.RemoveFavoriteComicAsync(comicKey);
                // TO-DO: In the future, scan the downloads folder and delete folders containing IDs that are no longer in the database.

                _logger.LogInformation(
                    "Comic '{Id}' from '{Source}' removed from favorites",
                    comicKey.Id,
                    comicKey.Source
                );
                return Result.Success();
            },
            "Error removing from favorites"
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
            "Error removing collection"
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
                    "Comic '{Id}' from '{Source}' removed from collection '{CollectionName}'",
                    comicKey.Id,
                    comicKey.Source,
                    collectionName
                );
                return Result.Success();
            },
            "Error removing comic from collection"
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
            "Error removing comic source"
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
            "Error cleaning up data"
        );
    }

    // --- Helpers && Private Methods ---

    private async Task<IReadOnlyList<ChapterModel>> FetchAndCacheChaptersAsync(
        ContentKey comicKey,
        string language,
        bool shouldSave = true,
        CancellationToken ct = default
    )
    {
        await LoadComicSourceAsync(comicKey.Source, ct);
        var chapters = await _srcService.GetAllChaptersAsync(comicKey.Id, language, ct);

        if (shouldSave)
            await _dbService.UpsertChaptersAsync(comicKey, language, chapters);

        return chapters;
    }

    private async Task LoadComicSourceAsync(string sourceName, CancellationToken ct = default)
    {
        var comicSource =
            (await _dbService.GetComicSourceDetailsAsync(sourceName, ct))
            ?? throw new InvalidOperationException(
                $"The source '{sourceName}' is not registered in the database."
            );

        if (!comicSource.IsEnabled)
        {
            _logger.LogWarning(
                "Attempted to load disabled comic source '{SourceName}'",
                sourceName
            );

            throw new ComicSourceDisabledException(
                $"The source '{sourceName}' is currently disabled. Please enable it in the settings."
            );
        }

        await _srcService.LoadSourceAsync(comicSource);
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
            return Result<T>.Failure("No internet connection or source is offline.", errorTitle);
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
            return Result.Failure("No internet connection or source is offline.", errorTitle);
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
}
