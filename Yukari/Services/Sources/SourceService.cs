using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Yukari.Core.Models;
using Yukari.Core.Sources;
using Yukari.Exceptions;
using Yukari.Helpers;
using Yukari.Models;

namespace Yukari.Services.Sources;

internal class SourceService : ISourceService
{
    private readonly ILogger<SourceService> _logger;

    private readonly ISharedHttpClient _sharedHttpClient;

    private readonly ConcurrentDictionary<string, Type> _sourceTypeCache = new();
    private readonly ConcurrentDictionary<string, IComicSource> _sourceInstances = new();

    public SourceService(
        ILogger<SourceService> logger,
        HttpClient httpClient
    )
    {
        _logger = logger;
        _sharedHttpClient = new SharedHttpClient(httpClient);
    }

    public async Task LoadSourceAsync(ComicSourceModel comicSource)
    {
        if (_sourceInstances.ContainsKey(comicSource.Name))
            return;

        try
        {
            _logger.LogInformation(
                "Loading comic source '{SourceName}' instance from {DllPath}",
                comicSource.Name,
                comicSource.DllPath
            );

            if (!_sourceTypeCache.TryGetValue(comicSource.Name, out var type))
            {
                // AssemblyLoadContext.Default is intentionally used instead of a collectible context.
                // Plugins are small and rarely removed during a session — the complexity of a collectible
                // context is not justified. DLL removal is handled by deferred deletion at next startup.
                type = await GetSourceTypeFromAssemblyAsync(comicSource.DllPath);
                _sourceTypeCache[comicSource.Name] = type;
            }

            var instance = (IComicSource)Activator.CreateInstance(type)!;

            if (instance is IRequiresHttpClient requiresHttpClient)
                requiresHttpClient.SetHttpClient(_sharedHttpClient);

            if (!_sourceInstances.TryAdd(comicSource.Name, instance))
                await instance.DisposeAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load source {comicSource.Name}", ex);
        }
    }

    public async Task UnloadSourceAsync(string sourceName)
    {
        if (_sourceInstances.TryRemove(sourceName, out var source))
            await source.DisposeAsync();

        _sourceTypeCache.TryRemove(sourceName, out _);
    }

    public IReadOnlyList<Filter> GetFilters(string sourceName) =>
        GetLoadedSource(sourceName).Filters;

    public IReadOnlyDictionary<string, string> GetLanguages(string sourceName) =>
        GetLoadedSource(sourceName).Languages;

    public async Task<IReadOnlyList<ComicModel>> SearchComicsAsync(
        string sourceName,
        string query,
        IReadOnlyDictionary<string, IReadOnlyList<string>> filters,
        int page = 1,
        CancellationToken ct = default
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);

        var comics = await GetLoadedSource(sourceName).SearchAsync(query, filters, page, ct);
        return comics.Select(c => MapToModel(c, sourceName)).ToList();
    }

    public async Task<IReadOnlyList<ComicModel>> GetTrendingComicsAsync(
        string sourceName,
        IReadOnlyDictionary<string, IReadOnlyList<string>> filters,
        int page = 1,
        CancellationToken ct = default
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);

        var comics = await GetLoadedSource(sourceName).GetTrendingAsync(filters, page, ct);
        return comics.Select(c => MapToModel(c, sourceName)).ToList();
    }

    public async Task<ComicModel?> GetComicDetailsAsync(
        string sourceName,
        string comicId,
        CancellationToken ct = default
    )
    {
        var comic = await GetLoadedSource(sourceName).GetDetailsAsync(comicId, ct);
        if (comic == null)
            return null;

        return MapToModel(comic, sourceName);
    }

    public async Task<IReadOnlyList<ChapterModel>> GetAllChaptersAsync(
        string sourceName,
        string comicId,
        string language,
        CancellationToken ct = default
    )
    {
        var chapters = await GetLoadedSource(sourceName).GetAllChaptersAsync(comicId, language, ct);
        return chapters
            .Select(c => new ChapterModel
            {
                Id = c.Id,
                Source = sourceName,
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
        string sourceName,
        string comicId,
        string chapterId,
        CancellationToken ct = default
    )
    {
        var pages = await GetLoadedSource(sourceName).GetChapterPagesAsync(comicId, chapterId, ct);
        return pages
            .Select(p => new ChapterPageModel
            {
                Number = p.Number,
                ImageUrl = SourceImageUrlHelper.Encode(sourceName, p.ImageUrl),
            })
            .ToList();
    }

    public async Task<ComicSourceModel> GetComicSourceModelFromAssemblyAsync(
        string dllPath,
        bool metadataOnly = false
    )
    {
        var type = await GetSourceTypeFromAssemblyAsync(dllPath, metadataOnly);
        var comicSourceMetadata =
            type.GetCustomAttribute<ComicSourceMetadataAttribute>()
            ?? throw new InvalidOperationException(
                $"{type.Name} is missing [ComicSourceMetadata] attribute."
            );

        if (!_sourceTypeCache.ContainsKey(comicSourceMetadata.Name) && !metadataOnly)
            _sourceTypeCache[comicSourceMetadata.Name] = type;

        _logger.LogInformation(
            "Plugin loaded from '{DllPath}' — Name: '{Name}', Version: {Version}",
            dllPath,
            comicSourceMetadata.Name,
            comicSourceMetadata.Version
        );
        return new ComicSourceModel
        {
            Name = comicSourceMetadata.Name,
            Version = comicSourceMetadata.Version,
            ReleasesPage = comicSourceMetadata.ReleasesPage,
            LogoUrl = TryEncodeImageUrl(comicSourceMetadata.Name, comicSourceMetadata.LogoUrl),
            Description = comicSourceMetadata.Description,
            DllPath = dllPath,
            IsEnabled = true,
        };
    }

    private string? TryEncodeImageUrl(string sourceName, string? imageUrl) =>
        imageUrl != null ? SourceImageUrlHelper.Encode(sourceName, imageUrl) : null;

    private ComicModel MapToModel(Comic coreComic, string sourceName) =>
        new()
        {
            Id = coreComic.Id,
            Source = sourceName,
            ComicUrl = coreComic.ComicUrl,
            Title = coreComic.Title,
            Author = coreComic.Author,
            Description = coreComic.Description,
            Status = coreComic.Status,
            Tags = coreComic.Tags,
            Year = coreComic.Year,
            CoverImageUrl = TryEncodeImageUrl(sourceName, coreComic.CoverImageUrl),
            Langs = CreateLanguageModelArray(GetLanguages(sourceName), coreComic.Langs),
        };

    private LanguageModel[] CreateLanguageModelArray(
        IReadOnlyDictionary<string, string> sourceLangs,
        string[] languageKeys
    )
    {
        return languageKeys
                ?.Select(key => new LanguageModel(
                    key,
                    sourceLangs.TryGetValue(key, out var displayName) ? displayName : key
                ))
                .ToArray()
            ?? [];
    }

    private IComicSource GetLoadedSource(string sourceName)
    {
        if (!_sourceInstances.TryGetValue(sourceName, out var source))
            throw new InvalidOperationException($"Source '{sourceName}' is not loaded.");
        return source;
    }

    private async Task<Type> GetSourceTypeFromAssemblyAsync(
        string pluginPath,
        bool collectibleContext = false
    )
    {
        if (!File.Exists(pluginPath))
            throw new FileNotFoundException();

        var context = collectibleContext
            ? new AssemblyLoadContext("CollectibleContext", isCollectible: true)
            : AssemblyLoadContext.Default;

        try
        {
            return await Task.Run(() =>
            {
                Assembly pluginAssembly = context.LoadFromAssemblyPath(pluginPath);
                var comicSourceType =
                    pluginAssembly
                        .GetTypes()
                        .FirstOrDefault(t =>
                            typeof(IComicSource).IsAssignableFrom(t)
                            && !t.IsInterface
                            && !t.IsAbstract
                        )
                    ?? throw new InvalidOperationException(
                        $"{pluginPath} does not implement IComicSource."
                    );

                if (!collectibleContext)
                    ValidateCoreCompatibility(Path.GetFileName(pluginPath), pluginAssembly);

                return comicSourceType;
            });
        }
        catch (Exception ex) when (ex is ReflectionTypeLoadException or TypeLoadException)
        {
            throw new PluginVersionMismatchException(Path.GetFileName(pluginPath));
        }
    }

    private void ValidateCoreCompatibility(string pluginPath, Assembly pluginAssembly)
    {
        var coreReference = pluginAssembly
            .GetReferencedAssemblies()
            .FirstOrDefault(a => a.Name == "Yukari.Core");

        if (coreReference == null)
            return;

        var requiredVersion = coreReference.Version;
        if (requiredVersion > AppInfoHelper.CoreVersion)
            throw new PluginVersionMismatchException(pluginPath);

        if (requiredVersion < AppInfoHelper.CoreVersion)
        {
            _logger.LogWarning(
                "Plugin '{pluginPath}' targets an older Core version ({RequiredCore}) and may lack features.",
                pluginPath,
                requiredVersion?.ToString(3)
            );
        }
    }
}
