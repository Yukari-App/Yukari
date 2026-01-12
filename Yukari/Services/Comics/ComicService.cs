using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Globalization;
using Yukari.Core.Models;
using Yukari.Models;
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

        public async Task<bool> UpsertFavoriteComicAsync(ComicModel comic, string selectedLanguage)
        {
            var success = await _dbService.UpsertFavoriteComicAsync(comic, selectedLanguage);
            if (!success) return false;

            await LoadComicSourceAsync(comic.Source);
            var chapters = await _srcService.GetAllChaptersAsync(comic.Id, selectedLanguage);

            return await _dbService.UpsertChaptersAsync(new(comic.Id, comic.Source), selectedLanguage, chapters);
        }

        public async Task<bool> UpsertComicUserDataAsync(ContentKey comicKey, ComicUserData comicUserData) =>
            await _dbService.UpsertComicUserDataAsync(comicKey, comicUserData);

        public async Task<bool> UpsertChapterUserDataAsync(ContentKey comicKey, ContentKey chapterKey, ChapterUserData chapterUserData) =>
            await _dbService.UpsertChapterUserDataAsync(comicKey, chapterKey, chapterUserData);

        public async Task<bool> UpsertComicSourceAsync(ComicSourceModel comicSource) =>
            await _dbService.UpsertComicSourceAsync(comicSource);

        public async Task<bool> RemoveFavoriteComicAsync(ContentKey comicKey)
        {
            var success = await _dbService.RemoveFavoriteComicAsync(comicKey);
            if (!success) return false;

            // TODO: In the future, scan the downloads folder and delete folders containing IDs that are no longer in the database.

            return true;
        }

        public async Task<bool> RemoveComicSourceAsync(string sourceName) =>
            await _dbService.RemoveComicSourceAsync(sourceName);

        public async Task<bool> CleanupUnfavoriteComicsDataAsync() => 
            await _dbService.CleanupUnfavoriteComicsDataAsync();

        private async Task<IReadOnlyList<ChapterModel>> FetchAndCacheChaptersAsync(ContentKey comicKey, string language, bool shouldSave = true)
        {
            await LoadComicSourceAsync(comicKey.Source);
            var chapters = await _srcService.GetAllChaptersAsync(comicKey.Id, language);

            if (shouldSave && chapters.Count > 0)
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
