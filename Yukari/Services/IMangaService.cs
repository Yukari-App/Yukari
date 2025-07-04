using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Models;

namespace Yukari.Services
{
    public interface IMangaService
    {
        Task<Manga?> GetMangaAsync(Guid id);
        Task<List<Manga>> GetFavoriteMangasAsync();
    }
}
