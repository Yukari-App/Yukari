using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Yukari.Core.Models;
using Yukari.Core.Sources;
using Yukari.Models;

namespace Yukari.Services.Sources
{
    internal class SourceService : ISourceService
    {
        private Dictionary<string, Type> _loadedSources = new();

        private IComicSource _currentSource;

        public async Task LoadSourceAsync(ComicSourceModel comicSource)
        {
            if (_currentSource?.Name == comicSource.Name) return;

            if (_currentSource != null)
                await _currentSource.DisposeAsync();

            if (!IsSourceLoaded(comicSource.Name))
            {
                if (!File.Exists(comicSource.DllPath))
                    throw new FileNotFoundException();

                _loadedSources[comicSource.Name] = GetSourceTypeFromAssembly(comicSource.DllPath);
            }

            _currentSource = InstantiateSource(_loadedSources[comicSource.Name])!;
        }

        public IReadOnlyList<Filter> GetFilters() => _currentSource.Filters;
        public IReadOnlyDictionary<string, string> GetLanguages() => _currentSource.Languages;

        public async Task<IReadOnlyList<ComicModel>> SearchComicsAsync(string query, IReadOnlyDictionary<string, IReadOnlyList<string>> filters)
        {
            var comics = await _currentSource.SearchAsync(query, filters);

            return comics.Select(c =>
                new ComicModel
                {
                    Id = c.Id,
                    Source = c.Source,
                    ComicUrl = c.ComicUrl,
                    Title = c.Title,
                    Author = c.Author,
                    Description = c.Description,
                    Tags = c.Tags,
                    Year = c.Year,
                    CoverImageUrl = c.CoverImageUrl,
                    Langs = CreateLanguageModelArray(c.Langs)
                }
            ).ToList();
        }

        public async Task<IReadOnlyList<ComicModel>> GetTrendingComicsAsync(IReadOnlyDictionary<string, IReadOnlyList<string>> filters)
        {
            var comics = await _currentSource.GetTrendingAsync(filters);

            return comics.Select(c =>
                new ComicModel
                {
                    Id = c.Id,
                    Source = c.Source,
                    ComicUrl = c.ComicUrl,
                    Title = c.Title,
                    Author = c.Author,
                    Description = c.Description,
                    Tags = c.Tags,
                    Year = c.Year,
                    CoverImageUrl = c.CoverImageUrl,
                    Langs = CreateLanguageModelArray(c.Langs)
                }
            ).ToList();
        }

        public async Task<ComicModel?> GetComicDetailsAsync(string comicId)
        {
            var comic = await _currentSource.GetDetailsAsync(comicId);

            if (comic == null) return null;
            return new ComicModel
            {
                Id = comic.Id,
                Source = comic.Source,
                ComicUrl = comic.ComicUrl,
                Title = comic.Title,
                Author = comic.Author,
                Description = comic.Description,
                Tags = comic.Tags,
                Year = comic.Year,
                CoverImageUrl = comic.CoverImageUrl,
                Langs = CreateLanguageModelArray(comic.Langs)
            };
        }

        public async Task<IReadOnlyList<ChapterModel>> GetAllChaptersAsync(string comicId, string language)
        {
            var chapters = await _currentSource.GetAllChaptersAsync(comicId, language);

            return chapters.Select(c =>
                new ChapterModel
                {
                    Id = c.Id,
                    ComicId = comicId,
                    Source = c.Source,
                    Title = c.Title,
                    Number = c.Number,
                    Volume = c.Volume,
                    Language = c.Language,
                    Groups = c.Groups,
                    LastUpdate = c.LastUpdate,
                    Pages = c.Pages
                }
            ).ToList();
        }

        public Task<IReadOnlyList<ChapterPageModel>> GetChapterPagesAsync(string chapterId)
        {
            throw new NotImplementedException();
        }

        public ComicSourceModel GetComicSourceModelFromAssembly(string dllPath)
        {
            if (!File.Exists(dllPath))
                throw new FileNotFoundException();

            var sourceInstance = InstantiateSource(GetSourceTypeFromAssembly(dllPath));

            return new ComicSourceModel
            {
                Name = sourceInstance.Name,
                Version = sourceInstance.Version,
                LogoUrl = sourceInstance.LogoUrl,
                Description = sourceInstance.Description,
                DllPath = dllPath,
                IsEnabled = true
            };
        }

        private bool IsSourceLoaded(string sourceName) => _loadedSources.ContainsKey(sourceName);

        private LanguageModel[] CreateLanguageModelArray(string[] languageKeys)
        {
            var sourceLangs = GetLanguages();

            return languageKeys?.Select(key => new LanguageModel(
                key,
                sourceLangs.TryGetValue(key, out var displayName) ? displayName : key
            )).ToArray() ?? [];
        }

        private Type GetSourceTypeFromAssembly(string pluginPath)
        {
            if (!File.Exists(pluginPath))
                throw new FileNotFoundException();

            Assembly pluginAssembly = Assembly.LoadFrom(pluginPath);

            return pluginAssembly.GetTypes()
                .FirstOrDefault(t => typeof(IComicSource).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                ?? throw new InvalidOperationException($"{pluginPath} does not implement IComicSource.");
        }

        private IComicSource InstantiateSource(Type pluginType) =>
            (IComicSource)Activator.CreateInstance(pluginType)!;
    }
}
