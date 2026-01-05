using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Core.Models;
using Yukari.Models;
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

        public async Task<IReadOnlyList<Filter>> GetSourceFiltersAsync(string? sourceName = null)
        {
            if (string.IsNullOrEmpty(sourceName)) return [];
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

            return string.IsNullOrEmpty(queryText) ? await _srcService.GetTrendingComicsAsync(filters)
                : await _srcService.SearchComicsAsync(queryText ?? string.Empty, filters);
        }

        public Task<IReadOnlyList<ComicModel>> GetFavoriteComicsAsync(string? queryText, string filter) =>
            _dbService.GetFavoriteComicsAsync(queryText);

        public async Task<ComicAggregate?> GetComicDetailsAsync(ContentKey comicKey)
        {
            var comic = await _dbService.GetComicDetailsAsync(comicKey);
            if (comic != null)
                return new(comic, await _dbService.GetComicUserDataAsync(comicKey));

            await LoadComicSourceAsync(comicKey.Source);
            comic = await _srcService.GetComicDetailsAsync(comicKey.Id);
            if (comic != null)
                return new(comic, await _dbService.GetComicUserDataAsync(comicKey));

            return null;
        }

        public async Task<IReadOnlyList<ChapterAggregate>> GetAllChaptersAsync(ContentKey comicKey, string language)
        {
            List<ChapterAggregate> chapters = new();

            var comicUserData = await _dbService.GetComicUserDataAsync(comicKey);
            if (comicUserData.IsFavorite)
            {
                var dbChapters = await _dbService.GetAllChaptersAsync(comicKey, language);
                if (dbChapters.Count > 0)
                    foreach (var chapter in dbChapters)
                        chapters.Add(new(chapter, await _dbService.GetChapterUserDataAsync(comicKey, new(chapter.Id, chapter.Source))));

                return chapters;
            }

            await LoadComicSourceAsync(comicKey.Source);
            var sourceChapters = await _srcService.GetAllChaptersAsync(comicKey.Id, language);
            if (sourceChapters.Count > 0)
                foreach (var chapter in sourceChapters)
                    chapters.Add(new(chapter, await _dbService.GetChapterUserDataAsync(comicKey, new(chapter.Id, chapter.Source))));

            return chapters;
        }

        public Task<IReadOnlyList<ChapterPageModel>> GetChapterPagesAsync(ContentKey chapterKey)
        {
            throw new System.NotImplementedException();
        }

        public async Task<IReadOnlyList<ComicSourceModel>> GetComicSourcesAsync() =>
            await _dbService.GetComicSourcesAsync();

        private async Task LoadComicSourceAsync(string sourceName)
        {
            var comicSource = await _dbService.GetComicSourceDetailsAsync(sourceName);
            if (comicSource != null)
                await _srcService.LoadSourceAsync(comicSource);
        }
    }
}
