using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Yukari.Core.Models;
using Yukari.Helpers;
using Yukari.Models;
using Yukari.Models.Common;
using Yukari.Models.Data;
using Yukari.Models.DTO;
using Yukari.Services.Sources;
using Yukari.Services.Storage;

namespace Yukari.Services.Comics
{
    internal class ComicService : IComicService
    {
        private readonly IDataService _dbService;
        private readonly ISourceService _srcService;
        private readonly IDownloadService _dloadService;

        public ComicService(
            IDataService dbService,
            ISourceService srcService,
            IDownloadService dloadService
        )
        {
            _dbService = dbService;
            _srcService = srcService;
            _dloadService = dloadService;
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
                    return _srcService.GetFilters();
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
                    return _srcService.GetLanguages();
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
                    await LoadComicSourceAsync(sourceName, ct);

                    return string.IsNullOrEmpty(queryText)
                        ? await _srcService.GetTrendingComicsAsync(filters, ct)
                        : await _srcService.SearchComicsAsync(queryText, filters, ct);
                },
                "Error fetching comics",
                ct
            );
        }

        public async Task<Result<IReadOnlyList<ComicModel>>> GetFavoriteComicsAsync(
            string? queryText,
            string filter,
            CancellationToken ct = default
        )
        {
            return await ExecuteAsync(
                (ct) => _dbService.GetFavoriteComicsAsync(queryText, ct),
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
                        return null;

                    return new ComicAggregate(comic, userData);
                },
                "Error fetching comic details",
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
                (ct) => _dbService.GetComicReadingProgressAsync(comicKey, language, ct),
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
                            chapters = await FetchAndCacheChaptersAsync(comicKey, language, ct: ct);
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

                    return chapters
                            .Select(c =>
                            {
                                var userData =
                                    userDataMap.GetValueOrDefault(c.Id) ?? new ChapterUserData();
                                return new ChapterAggregate(c, userData);
                            })
                            .ToList() as IReadOnlyList<ChapterAggregate>;
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
                (ct) => _dbService.GetChapterUserDataAsync(comicKey, chapterKey, ct),
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
                        pages = await _srcService.GetChapterPagesAsync(
                            comicKey.Id,
                            chapterKey.Id,
                            ct
                        );
                    }

                    return pages;
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
                (ct) => _dbService.GetComicSourcesAsync(ct),
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
                            throw new InvalidOperationException(
                                "The comic doesn't exist in the source and cannot be favorited."
                            );

                        comicDetails =
                            await _dbService.GetComicDetailsAsync(comicKey)
                            ?? throw new InvalidOperationException(
                                "Comic not found locally for update."
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
                () => _dbService.UpsertComicUserDataAsync(comicKey, comicUserData),
                "Error saving progress"
            );
        }

        public async Task<Result> UpsertComicReadingProgressAsync(
            ContentKey comicKey,
            ComicReadingProgress progress
        )
        {
            return await ExecuteAsync(
                () => _dbService.UpsertComicReadingProgressAsync(comicKey, progress),
                "Error saving comic reading progress"
            );
        }

        public async Task<Result> UpsertChaptersAsync(ContentKey comicKey, string language)
        {
            return await ExecuteAsync(
                async () =>
                {
                    await FetchAndCacheChaptersAsync(comicKey, language);
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
                () => _dbService.UpsertChapterUserDataAsync(comicKey, chapterKey, chapterUserData),
                "Error saving chapter progress"
            );
        }

        public async Task<Result> UpsertChaptersIsReadAsync(
            ContentKey comicKey,
            string[] chapterIDs,
            bool IsRead
        )
        {
            return await ExecuteAsync(
                () => _dbService.UpsertChaptersIsReadAsync(comicKey, chapterIDs, IsRead),
                "Error setting read status"
            );
        }

        public async Task<Result> UpsertComicSourceAsync(string pluginPath, bool isEnabled = true)
        {
            try
            {
                var comicSource = _srcService.GetComicSourceModelFromAssembly(pluginPath);
                comicSource.DllPath = AppDataHelper.CopyDllToPluginsDirectory(pluginPath);
                comicSource.IsEnabled = isEnabled;

                await _dbService.UpsertComicSourceAsync(comicSource);
                return Result.Success();
            }
            catch (IOException)
            {
                return Result.Failure(
                    "The source is already in use and cannot be updated now. Restart Yukari and try again."
                );
            }
            catch (Exception ex)
            {
                return Result.Failure($"Error saving comic source: {ex.Message}");
            }
        }

        public async Task<Result> RemoveFavoriteComicAsync(ContentKey comicKey)
        {
            return await ExecuteAsync(
                async () =>
                {
                    await _dbService.RemoveFavoriteComicAsync(comicKey);
                    // TO-DO: In the future, scan the downloads folder and delete folders containing IDs that are no longer in the database.
                },
                "Error removing from favorites"
            );
        }

        public async Task<Result> RemoveComicSourceAsync(string sourceName)
        {
            return await ExecuteAsync(
                () => _dbService.RemoveComicSourceAsync(sourceName),
                "Error removing comic source"
            );
        }

        public async Task<Result> CleanupUnfavoriteComicsDataAsync()
        {
            return await ExecuteAsync(
                () => _dbService.CleanupUnfavoriteComicsDataAsync(),
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
                throw new InvalidOperationException(
                    $"The source '{sourceName}' is currently disabled. Please enable it in the settings."
                );

            await _srcService.LoadSourceAsync(comicSource);
        }

        private async Task<Result<T>> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> action,
            string errorMessagePrefix,
            CancellationToken ct = default
        )
        {
            try
            {
                return Result<T>.Success(await action(ct));
            }
            catch (OperationCanceledException)
            {
                return Result<T>.Cancelled();
            }
            catch (Exception ex) when (IsNetworkError(ex))
            {
                return Result<T>.Failure(
                    $"{errorMessagePrefix}: No internet connection or source is offline."
                );
            }
            catch (Exception ex)
            {
                // Here I can add global logging in the future.
                var message = ex.InnerException?.Message ?? ex.Message;
                return Result<T>.Failure($"{errorMessagePrefix}: {message}");
            }
        }

        private async Task<Result> ExecuteAsync(Func<Task> action, string errorMessagePrefix)
        {
            try
            {
                await action();
                return Result.Success();
            }
            catch (Exception ex) when (IsNetworkError(ex))
            {
                return Result.Failure(
                    $"{errorMessagePrefix}: No internet connection or source is offline."
                );
            }
            catch (Exception ex)
            {
                var message = ex.InnerException?.Message ?? ex.Message;
                return Result.Failure($"{errorMessagePrefix}: {message}");
            }
        }

        private bool IsNetworkError(Exception ex)
        {
            return ex is System.Net.Http.HttpRequestException
                || ex is System.Net.Sockets.SocketException
                || ex.InnerException is System.Net.Http.HttpRequestException;
        }
    }
}
