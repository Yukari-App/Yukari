using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public ComicService(IDataService dbService, ISourceService srcService)
        {
            _dbService = dbService;
            _srcService = srcService;
        }

        // --- Read Methods ---

        public async Task<Result<IReadOnlyList<Filter>>> GetSourceFiltersAsync(string sourceName)
        {
            return await ExecuteAsync(async () =>
            {
                await LoadComicSourceAsync(sourceName);
                return _srcService.GetFilters();
            }, "Error getting source filters");
        }

        public async Task<Result<IReadOnlyDictionary<string, string>>> GetSourceLanguagesAsync(string sourceName)
        {
            return await ExecuteAsync(async () =>
            {
                await LoadComicSourceAsync(sourceName);
                return _srcService.GetLanguages();
            }, "Error getting source languages");
        }

        public async Task<Result<IReadOnlyList<ComicModel>>> SearchComicsAsync(string sourceName, string? queryText, IReadOnlyDictionary<string, IReadOnlyList<string>> filters)
        {
            return await ExecuteAsync(async () =>
            {
                await LoadComicSourceAsync(sourceName);

                return string.IsNullOrEmpty(queryText)
                    ? await _srcService.GetTrendingComicsAsync(filters)
                    : await _srcService.SearchComicsAsync(queryText, filters);
            }, "Error fetching comics");
        }

        public async Task<Result<IReadOnlyList<ComicModel>>> GetFavoriteComicsAsync(string? queryText, string filter)
        {
            return await ExecuteAsync(
                () => _dbService.GetFavoriteComicsAsync(queryText), 
                "Error fetching favorite comics");
        }

        public async Task<Result<ComicAggregate?>> GetComicDetailsAsync(ContentKey comicKey, bool forceWeb = false)
        {
            return await ExecuteAsync(async () =>
            {
                var userData = await _dbService.GetComicUserDataAsync(comicKey);
                ComicModel? comic;

                if (userData.IsFavorite && !forceWeb)
                {
                    comic = await _dbService.GetComicDetailsAsync(comicKey);
                }
                else
                {
                    await LoadComicSourceAsync(comicKey.Source);
                    comic = await _srcService.GetComicDetailsAsync(comicKey.Id);
                }

                if (comic == null) return null;

                return new ComicAggregate(comic, userData);
            }, "Error fetching comic details");
        }

        public async Task<Result<IReadOnlyList<ChapterAggregate>>> GetAllChaptersAsync(ContentKey comicKey, string language, bool forceWeb = false)
        {
            return await ExecuteAsync(async () =>
            {
                var userDataMap = await _dbService.GetAllChaptersUserDataMapAsync(comicKey);
                IReadOnlyList<ChapterModel> chapters;
                var comicUserData = await _dbService.GetComicUserDataAsync(comicKey);

                if (comicUserData.IsFavorite && !forceWeb)
                {
                    chapters = await _dbService.GetAllChaptersAsync(comicKey, language);
                    if (chapters.Count == 0)
                        chapters = await FetchAndCacheChaptersAsync(comicKey, language);
                }
                else
                {
                    chapters = await FetchAndCacheChaptersAsync(comicKey, language, comicUserData.IsFavorite);
                }

                return chapters.Select(c =>
                {
                    var userData = userDataMap.GetValueOrDefault(c.Id) ?? new ChapterUserData();
                    return new ChapterAggregate(c, userData);
                }).ToList() as IReadOnlyList<ChapterAggregate>;

            }, "Error fetching chapters");
        }

        public async Task<Result<IReadOnlyList<ChapterPageModel>>> GetChapterPagesAsync(ContentKey comicKey, ContentKey chapterKey, bool forceWeb = false)
        {
            return await ExecuteAsync(async () =>
            {
                var chapterUserData = await _dbService.GetChapterUserDataAsync(comicKey, chapterKey);
                IReadOnlyList<ChapterPageModel> pages;

                if (chapterUserData.IsDownloaded && !forceWeb)
                {
                    pages = await _dbService.GetChapterPagesAsync(comicKey, chapterKey);
                    if (pages.Count == 0)
                        throw new InvalidOperationException("Chapter marked as downloaded, but no pages found locally. Try downloading again.");
                }
                else
                {
                    await LoadComicSourceAsync(comicKey.Source);
                    pages =  await _srcService.GetChapterPagesAsync(comicKey.Id, chapterKey.Id);
                }

                return pages;
            }, "Error fetching chapter pages");
        }

        public async Task<Result<IReadOnlyList<ComicSourceModel>>> GetComicSourcesAsync()
        {
            return await ExecuteAsync(
                () => _dbService.GetComicSourcesAsync(),
                "Error fetching comic sources");
        }

        // --- Write Methods ---

        public async Task<Result> UpsertFavoriteComicAsync(ContentKey comicKey)
        {
            return await ExecuteAsync(async () =>
            {
                await LoadComicSourceAsync(comicKey.Source);
                var comicDetails = await _srcService.GetComicDetailsAsync(comicKey.Id);

                if (comicDetails == null)
                {
                    var comicUserData = await _dbService.GetComicUserDataAsync(comicKey);
                    if (!comicUserData.IsFavorite)
                        throw new InvalidOperationException("The comic doesn't exist in the source and cannot be favorited.");

                    comicDetails = await _dbService.GetComicDetailsAsync(comicKey)
                                   ?? throw new InvalidOperationException("Comic not found locally for update.");

                    comicDetails.IsAvailable = false;
                }

                await _dbService.UpsertFavoriteComicAsync(comicDetails);
            }, "Failed to add to favorites");
        }
            
        public async Task<Result> UpsertComicUserDataAsync(ContentKey comicKey, ComicUserData comicUserData)
        {
            return await ExecuteAsync(
                () => _dbService.UpsertComicUserDataAsync(comicKey, comicUserData),
                "Error saving progress");
        }

        public async Task<Result> UpsertChaptersAsync(ContentKey comicKey, string language)
        {
            return await ExecuteAsync(async () =>
            {
                await FetchAndCacheChaptersAsync(comicKey, language);
            }, "Error persisting chapters");
        }

        public async Task<Result> UpsertChapterUserDataAsync(ContentKey comicKey, ContentKey chapterKey, ChapterUserData chapterUserData)
        {
            return await ExecuteAsync(
                () => _dbService.UpsertChapterUserDataAsync(comicKey, chapterKey, chapterUserData),
                "Error saving chapter progress");
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
                return Result.Failure("The source is already in use and cannot be updated now. Restart Yukari and try again.");
            }
            catch (Exception ex)
            {
                return Result.Failure($"Error saving comic source: {ex.Message}");
            }
        }

        public async Task<Result> RemoveFavoriteComicAsync(ContentKey comicKey)
        {
            return await ExecuteAsync(async () =>
            {
                await _dbService.RemoveFavoriteComicAsync(comicKey);
                // TO-DO: In the future, scan the downloads folder and delete folders containing IDs that are no longer in the database.
            }, "Error removing from favorites");
        }

        public async Task<Result> RemoveComicSourceAsync(string sourceName)
        {
            return await ExecuteAsync(
                () => _dbService.RemoveComicSourceAsync(sourceName),
                "Error removing comic source");
        }
            
        public async Task<Result> CleanupUnfavoriteComicsDataAsync()
        {
            return await ExecuteAsync(
                () => _dbService.CleanupUnfavoriteComicsDataAsync(),
                "Error cleaning up data");
        }

        // --- Helpers && Private Methods ---

        private async Task<IReadOnlyList<ChapterModel>> FetchAndCacheChaptersAsync(ContentKey comicKey, string language, bool shouldSave = true)
        {
            await LoadComicSourceAsync(comicKey.Source);
            var chapters = await _srcService.GetAllChaptersAsync(comicKey.Id, language);

            if (shouldSave)
                await _dbService.UpsertChaptersAsync(comicKey, language, chapters);

            return chapters;
        }

        private async Task LoadComicSourceAsync(string sourceName)
        {
            var comicSource = (await _dbService.GetComicSourceDetailsAsync(sourceName))
                ?? throw new InvalidOperationException($"The source '{sourceName}' is not registered in the database.");
            await _srcService.LoadSourceAsync(comicSource);
        }

        private async Task<Result<T>> ExecuteAsync<T>(Func<Task<T>> action, string errorMessagePrefix)
        {
            try
            {
                var result = await action();
                return Result<T>.Success(result);
            }
            catch (Exception ex) when (IsNetworkError(ex))
            {
                return Result<T>.Failure($"{errorMessagePrefix}: No internet connection or source is offline.");
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
                return Result.Failure($"{errorMessagePrefix}: No internet connection or source is offline.");
            }
            catch (Exception ex)
            {
                var message = ex.InnerException?.Message ?? ex.Message;
                return Result.Failure($"{errorMessagePrefix}: {message}");
            }
        }

        private bool IsNetworkError(Exception ex)
        {
            return ex is System.Net.Http.HttpRequestException ||
                   ex is System.Net.Sockets.SocketException ||
                   ex.InnerException is System.Net.Http.HttpRequestException;
        }
    }
}
