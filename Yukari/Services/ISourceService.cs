using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Core.Models;
using Yukari.Models;

namespace Yukari.Services
{
    internal interface ISourceService
    {
        Task LoadSource(string sourceName);

        IReadOnlyList<Filter> GetFilters();
        IReadOnlyDictionary<string, string> GetLanguages();

        Task<IReadOnlyList<ComicModel>> SearchComicsAsync(string query, Dictionary<string, List<string>> filters);
        Task<IReadOnlyList<ComicModel>> GetTrendingComicsAsync(Dictionary<string, List<string>> filters);
        Task<ComicModel?> GetComicDetailsAsync(string comicId);
        Task<IReadOnlyList<ChapterModel>> GetAllChaptersAsync(string chapterId, string language);
        Task<IReadOnlyList<ChapterPageModel>> GetChapterPagesAsync(string chapterId);
    }
}
