using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Models;

namespace Yukari.Services
{
    internal interface ISqliteService
    {
        Task<Manga?> GetMangaByIdAsync(Guid id);
        Task<List<Manga>> GetFavoriteMangasAsync();
    }
}
