using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Models;
using Yukari.Models.Data;
using Yukari.Models.DTO;

namespace Yukari.Services.Storage
{
    public interface IDataService
    {
        Task<IReadOnlyList<ComicModel>> GetFavoriteComicsAsync(string? queryText = null);
        Task<ComicModel?> GetComicDetailsAsync(ContentKey ComicKey);
        Task<ComicUserData> GetComicUserDataAsync(ContentKey ComicKey);
        Task<IReadOnlyList<ChapterModel>> GetAllChaptersAsync(ContentKey ComicKey, string language);
        Task<Dictionary<string, ChapterUserData>> GetAllChaptersUserDataMapAsync(ContentKey comicKey);
        Task<ChapterUserData> GetChapterUserDataAsync(ContentKey comicKey, ContentKey chapterKey);
        Task<IReadOnlyList<ChapterPageModel>> GetChapterPagesAsync(ContentKey chapterKey);
        Task<IReadOnlyList<ComicSourceModel>> GetComicSourcesAsync();
        Task<ComicSourceModel?> GetComicSourceDetailsAsync(string sourceName);

        Task<bool> InsertFavoriteComicAsync(ComicModel comic);
        Task<bool> UpsertComicUserDataAsync(ContentKey comicKey, ComicUserData comicUserData);
        Task<bool> UpsertChapterAsync(ChapterModel chapter);
        Task<bool> UpsertChapterUserDataAsync(ContentKey comicKey, ContentKey chapterKey, ChapterUserData chapterUserData);
        Task<bool> UpsertChapterPagesAsync(IReadOnlyList<ChapterPageModel> chapterPages);
        Task<bool> UpsertComicSourceAsync(ComicSourceModel comicSource);

        Task<bool> RemoveFavoriteComicAsync(ContentKey ComicKey);
        Task<bool> RemoveChapterAsync(ContentKey chapterKey);
        Task<bool> RemoveComicSourceAsync(string sourceName);
        Task<bool> CleanupUnfavoriteComicsDataAsync();
    }
}
