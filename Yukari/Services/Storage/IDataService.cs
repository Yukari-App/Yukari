using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Models;

namespace Yukari.Services.Storage
{
    public interface IDataService
    {
        Task<IReadOnlyList<ComicModel>> GetFavoriteComicsAsync(string? queryText = null);
        Task<ComicModel?> GetComicDetailsAsync(ContentIdentifier comicIdentifier);
        Task<IReadOnlyList<ChapterModel>> GetAllChaptersAsync(ContentIdentifier comicIdentifier, string language);
        Task<IReadOnlyList<ChapterPageModel>> GetChapterPagesAsync(ContentIdentifier chapterIdentifier);

        Task<IReadOnlyList<ComicSourceModel>> GetComicSourcesAsync();
        ComicSourceModel? GetComicSourceDetails(string sourceName);
    }
}
