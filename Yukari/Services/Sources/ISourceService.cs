using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Yukari.Core.Models;
using Yukari.Models;

namespace Yukari.Services.Sources;

public interface ISourceService
{
    Task LoadSourceAsync(ComicSourceModel comicSource);

    IReadOnlyList<Filter> GetFilters();
    IReadOnlyDictionary<string, string> GetLanguages();

    Task<IReadOnlyList<ComicModel>> SearchComicsAsync(
        string query,
        IReadOnlyDictionary<string, IReadOnlyList<string>> filters,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<ComicModel>> GetTrendingComicsAsync(
        IReadOnlyDictionary<string, IReadOnlyList<string>> filters,
        CancellationToken ct = default
    );
    Task<ComicModel?> GetComicDetailsAsync(string comicId, CancellationToken ct = default);
    Task<IReadOnlyList<ChapterModel>> GetAllChaptersAsync(
        string comicId,
        string language,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<ChapterPageModel>> GetChapterPagesAsync(
        string comicId,
        string chapterId,
        CancellationToken ct = default
    );

    ComicSourceModel GetComicSourceModelFromAssembly(string dllPath);
}
