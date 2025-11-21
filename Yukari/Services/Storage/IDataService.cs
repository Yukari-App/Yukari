using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Models;
using Yukari.Models.DTO;

namespace Yukari.Services.Storage
{
    public interface IDataService
    {
        Task<IReadOnlyList<ComicModel>> GetFavoriteComicsAsync(string? queryText = null);
        Task<ComicModel?> GetComicDetailsAsync(ContentKey ComicKey);
        Task<IReadOnlyList<ChapterModel>> GetAllChaptersAsync(ContentKey ComicKey, string language);
        Task<IReadOnlyList<ChapterPageModel>> GetChapterPagesAsync(ContentKey chapterKey);

        Task<IReadOnlyList<ComicSourceModel>> GetComicSourcesAsync();
        ComicSourceModel? GetComicSourceDetails(string sourceName);
    }
}
