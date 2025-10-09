using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Core.Models;
using Yukari.Models;

namespace Yukari.Services
{
    internal interface ISourceService
    {
        void LoadSource(string sourceName);

        IReadOnlyList<Filter> GetFilters();
        IReadOnlyDictionary<string, string> GetLanguages();
        Task<IReadOnlyList<ComicModel>> SearchComicsAsync(string query, Dictionary<string, List<string>> filters);
        Task<IReadOnlyList<ComicModel>> GetTrendingComicsAsync(Dictionary<string, List<string>> filters);
        Task<ComicModel?> GetComicDetailsAsync(string mangaId);
        Task<IReadOnlyList<ChapterModel>> GetAllChaptersAsync(string mangaId, string language);
        Task<IReadOnlyList<ChapterPageModel>> GetChapterPagesAsync(string chapterId);
    }
}
