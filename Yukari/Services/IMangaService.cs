using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Models;

namespace Yukari.Services
{
    public interface IMangaService
    {
        Task<Manga?> GetMangaByIdAsync(Guid id);
        Task<List<Manga>> GetFavoriteMangasAsync();
    }
}
