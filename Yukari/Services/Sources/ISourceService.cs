using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Yukari.Core.Models;
using Yukari.Models;

namespace Yukari.Services.Sources;

public interface ISourceService
{
    Task LoadSourceAsync(ComicSourceModel comicSource);
    Task UnloadSourceAsync(string sourceName);

    Task<IReadOnlyList<Filter>> GetFiltersAsync(string sourceName, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, string>> GetLanguagesAsync(
        string sourceName,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<ComicModel>> SearchComicsAsync(
        string sourceName,
        string query,
        IReadOnlyDictionary<string, IReadOnlyList<string>> filters,
        int page = 1,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<ComicModel>> GetTrendingComicsAsync(
        string sourceName,
        IReadOnlyDictionary<string, IReadOnlyList<string>> filters,
        int page = 1,
        CancellationToken ct = default
    );
    Task<ComicModel?> GetComicDetailsAsync(
        string sourceName,
        string comicId,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<ChapterModel>> GetAllChaptersAsync(
        string sourceName,
        string comicId,
        string language,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<ChapterPageModel>> GetChapterPagesAsync(
        string sourceName,
        string comicId,
        string chapterId,
        CancellationToken ct = default
    );
    Task<byte[]?> GetImageBytesAsync(
        string sourceName,
        string imageUrl,
        CancellationToken ct = default
    );

    Task<ComicSourceModel> GetComicSourceModelFromAssemblyAsync(
        string dllPath,
        bool disposableContext = false
    );
}
