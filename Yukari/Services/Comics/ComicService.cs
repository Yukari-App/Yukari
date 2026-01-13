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

        public async Task<IReadOnlyList<Filter>> GetSourceFiltersAsync(string sourceName)
        {
            await LoadComicSourceAsync(sourceName);
            return _srcService.GetFilters();
        }

        public async Task<IReadOnlyDictionary<string, string>> GetSourceLanguagesAsync(string sourceName)
        {
            await LoadComicSourceAsync(sourceName);
            return _srcService.GetLanguages();
        }

        public async Task<IReadOnlyList<ComicModel>> SearchComicsAsync(string sourceName, string? queryText, IReadOnlyDictionary<string, IReadOnlyList<string>> filters)
        {
            await LoadComicSourceAsync(sourceName);
            return string.IsNullOrEmpty(queryText)
                ? await _srcService.GetTrendingComicsAsync(filters)
                : await _srcService.SearchComicsAsync(queryText, filters);
        }

        public Task<IReadOnlyList<ComicModel>> GetFavoriteComicsAsync(string? queryText, string filter) =>
            _dbService.GetFavoriteComicsAsync(queryText);

        public async Task<ComicAggregate?> GetComicDetailsAsync(ContentKey comicKey, bool forceWeb = false)
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

            if (comic != null)
                return new(comic, userData);

            return null;
        }

        public async Task<IReadOnlyList<ChapterAggregate>> GetAllChaptersAsync(ContentKey comicKey, string language, bool forceWeb = false)
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
                if (!userDataMap.TryGetValue(c.Id, out var userData))
                {
                    userData = new ChapterUserData();
                }

                return new ChapterAggregate(c, userData);
            }).ToList();
        }

        public Task<IReadOnlyList<ChapterPageModel>> GetChapterPagesAsync(ContentKey chapterKe, bool forceWeb = false)
        {
            throw new NotImplementedException();
        }

        public async Task<IReadOnlyList<ComicSourceModel>> GetComicSourcesAsync() =>
            await _dbService.GetComicSourcesAsync();

        public async Task<Result> UpsertFavoriteComicAsync(ContentKey comicKey)
        {
            try
            {
                await LoadComicSourceAsync(comicKey.Source);
                var comicDetails = await _srcService.GetComicDetailsAsync(comicKey.Id);

                if (comicDetails == null)
                {
                    var comicUserData = await _dbService.GetComicUserDataAsync(comicKey);
                    if (comicUserData.IsFavorite)
                    {
                        comicDetails = await _dbService.GetComicDetailsAsync(comicKey);
                        if (comicDetails == null)
                            return Result.Failure("Comic not found locally for update.");

                        comicDetails.IsAvailable = false;
                    }
                    else
                    {
                        return Result.Failure("The comic doesn't exist in the source and cannot be favorited.");
                    }
                }

                await _dbService.UpsertFavoriteComicAsync(comicDetails);
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Failed to add to favorites: {ex.Message}");
            }
        }
            
        public async Task<Result> UpsertComicUserDataAsync(ContentKey comicKey, ComicUserData comicUserData)
        {
            try
            {
                await _dbService.UpsertComicUserDataAsync(comicKey, comicUserData);
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Error saving progress: {ex.Message}");
            }
        }

        public async Task<Result> UpsertChaptersAsync(ContentKey comicKey, string language)
        {
            try
            {
                await FetchAndCacheChaptersAsync(comicKey, language);
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Error persisting chapters: {ex.Message}");
            }
        }

        public async Task<Result> UpsertChapterUserDataAsync(ContentKey comicKey, ContentKey chapterKey, ChapterUserData chapterUserData)
        {
            try
            {
                await _dbService.UpsertChapterUserDataAsync(comicKey, chapterKey, chapterUserData);
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Error saving chapter progress: {ex.Message}");
            }
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
            catch (Exception ex)
            {
                if (ex is IOException)
                {
                    return Result.Failure("The source is already in use and cannot be updated now. Restart Yukari and try again.");
                }
                return Result.Failure($"Error saving comic source: {ex.Message}");
            }
        }

        public async Task<Result> RemoveFavoriteComicAsync(ContentKey comicKey)
        {
            try
            {
                await _dbService.RemoveFavoriteComicAsync(comicKey);

                // TODO: In the future, scan the downloads folder and delete folders containing IDs that are no longer in the database.

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Error removing from favorites: {ex.Message}");
            }
        }

        public async Task<Result> RemoveComicSourceAsync(string sourceName)
        {
            try
            {
                await _dbService.RemoveComicSourceAsync(sourceName);
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Error removing comic source: {ex.Message}");
            }
        }
            

        public async Task<Result> CleanupUnfavoriteComicsDataAsync()
        {
            try
            {
                await _dbService.CleanupUnfavoriteComicsDataAsync();
                return Result.Success();

            }
            catch (Exception ex)
            {
                return Result.Failure($"Error cleaning up data: {ex.Message}");
            }
        }

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
    }
}
