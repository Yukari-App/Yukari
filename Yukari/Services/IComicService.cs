using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Models;

namespace Yukari.Services
{
    internal interface IComicService
    {
        Task<ComicModel?> GetComicByIdAsync(string id);
        Task<List<ComicModel>> GetComicsAsync(string? queryText = null);
        Task<List<ComicModel>> GetFavoriteComicsAsync(string? queryText = null);
        Task<ChapterModel> GetChapterAsync(string chapterId);
        Task<List<ChapterModel>> GetAllChaptersAsync(string mangaId);
        Task<List<ComicSourceModel>> GetComicSourcesAsync();
    }
}
