using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Yukari.Core.Models;
using Yukari.Core.Sources;
using Yukari.Models;

namespace Yukari.Services.Sources;

internal class SourceService : ISourceService
{
    private Dictionary<string, Type> _sourceTypeCache = new();

    private IComicSource? _currentSource;
    private string? _currentSourceName;

    public async Task LoadSourceAsync(ComicSourceModel comicSource)
    {
        if (_currentSourceName == comicSource.Name)
            return;

        if (_currentSource != null)
        {
            await _currentSource.DisposeAsync();
            _currentSource = null;
            _currentSourceName = null;
        }

        try
        {
            if (!_sourceTypeCache.TryGetValue(comicSource.Name, out var type))
            {
                type = GetSourceTypeFromAssembly(comicSource.DllPath);
                _sourceTypeCache[comicSource.Name] = type;
            }

            _currentSource = (IComicSource)Activator.CreateInstance(type)!;
            _currentSourceName = comicSource.Name;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load source {comicSource.Name}", ex);
        }
    }

    public IReadOnlyList<Filter> GetFilters() => _currentSource?.Filters ?? Array.Empty<Filter>();

    public IReadOnlyDictionary<string, string> GetLanguages() =>
        _currentSource?.Languages ?? new Dictionary<string, string>();

    public async Task<IReadOnlyList<ComicModel>> SearchComicsAsync(
        string query,
        IReadOnlyDictionary<string, IReadOnlyList<string>> filters,
        CancellationToken ct = default
    )
    {
        if (_currentSource == null)
            throw new InvalidOperationException("No source loaded.");

        var comics = await _currentSource.SearchAsync(query, filters, ct);
        return comics.Select(MapToModel).ToList();
    }

    public async Task<IReadOnlyList<ComicModel>> GetTrendingComicsAsync(
        IReadOnlyDictionary<string, IReadOnlyList<string>> filters,
        CancellationToken ct = default
    )
    {
        if (_currentSource == null)
            throw new InvalidOperationException("No source loaded.");

        var comics = await _currentSource.GetTrendingAsync(filters, ct);
        return comics.Select(MapToModel).ToList();
    }

    public async Task<ComicModel?> GetComicDetailsAsync(
        string comicId,
        CancellationToken ct = default
    )
    {
        if (_currentSource == null)
            throw new InvalidOperationException("No source loaded.");

        var comic = await _currentSource.GetDetailsAsync(comicId, ct);
        if (comic == null)
            return null;

        return MapToModel(comic);
    }

    public async Task<IReadOnlyList<ChapterModel>> GetAllChaptersAsync(
        string comicId,
        string language,
        CancellationToken ct = default
    )
    {
        if (_currentSource == null)
            throw new InvalidOperationException("No source loaded.");

        var chapters = await _currentSource.GetAllChaptersAsync(comicId, language, ct);
        return chapters
            .Select(c => new ChapterModel
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
                Pages = c.Pages,
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ChapterPageModel>> GetChapterPagesAsync(
        string comicId,
        string chapterId,
        CancellationToken ct = default
    )
    {
        if (_currentSource == null)
            throw new InvalidOperationException("No source loaded.");

        var pages = await _currentSource.GetChapterPagesAsync(comicId, chapterId, ct);
        return pages
            .Select(p => new ChapterPageModel
            {
                Id = p.Id,
                ChapterId = chapterId,
                Source = p.Source,
                PageNumber = p.PageNumber,
                ImageUrl = p.ImageUrl,
            })
            .ToList();
    }

    public ComicSourceModel GetComicSourceModelFromAssembly(string dllPath)
    {
        var type = GetSourceTypeFromAssembly(dllPath);
        var comicSourceMetadata =
            type.GetCustomAttribute<ComicSourceMetadataAttribute>()
            ?? throw new InvalidOperationException(
                $"{type.Name} is missing [ComicSourceMetadata] attribute."
            );

        if (!_sourceTypeCache.ContainsKey(comicSourceMetadata.Name))
            _sourceTypeCache[comicSourceMetadata.Name] = type;

        return new ComicSourceModel
        {
            Name = comicSourceMetadata.Name,
            Version = comicSourceMetadata.Version,
            LogoUrl = comicSourceMetadata.LogoUrl,
            Description = comicSourceMetadata.Description,
            DllPath = dllPath,
            IsEnabled = true,
        };
    }

    private ComicModel MapToModel(Comic coreComic) =>
        new()
        {
            Id = coreComic.Id,
            Source = coreComic.Source,
            ComicUrl = coreComic.ComicUrl,
            Title = coreComic.Title,
            Author = coreComic.Author,
            Description = coreComic.Description,
            Tags = coreComic.Tags,
            Year = coreComic.Year,
            CoverImageUrl = coreComic.CoverImageUrl,
            Langs = CreateLanguageModelArray(coreComic.Langs),
        };

    private LanguageModel[] CreateLanguageModelArray(string[] languageKeys)
    {
        var sourceLangs = GetLanguages();

        return languageKeys
                ?.Select(key => new LanguageModel(
                    key,
                    sourceLangs.TryGetValue(key, out var displayName) ? displayName : key
                ))
                .ToArray()
            ?? [];
    }

    private Type GetSourceTypeFromAssembly(string pluginPath)
    {
        if (!File.Exists(pluginPath))
            throw new FileNotFoundException();

        Assembly pluginAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(pluginPath);

        return pluginAssembly
                .GetTypes()
                .FirstOrDefault(t =>
                    typeof(IComicSource).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract
                )
            ?? throw new InvalidOperationException(
                $"{pluginPath} does not implement IComicSource."
            );
    }
}
