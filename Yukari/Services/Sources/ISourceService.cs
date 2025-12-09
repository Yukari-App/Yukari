using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Core.Models;
using Yukari.Models;

namespace Yukari.Services.Sources
{
    public interface ISourceService
    {
        Task LoadSourceAsync(ComicSourceModel comicSource);

        IReadOnlyList<Filter> GetFilters();
        IReadOnlyDictionary<string, string> GetLanguages();

        Task<IReadOnlyList<ComicModel>> SearchComicsAsync(string query, IReadOnlyDictionary<string, IReadOnlyList<string>> filters);
        Task<IReadOnlyList<ComicModel>> GetTrendingComicsAsync(IReadOnlyDictionary<string, IReadOnlyList<string>> filters);
        Task<ComicModel?> GetComicDetailsAsync(string comicId);
        Task<IReadOnlyList<ChapterModel>> GetAllChaptersAsync(string comicId, string language);
        Task<IReadOnlyList<ChapterPageModel>> GetChapterPagesAsync(string chapterId);
    }
}
