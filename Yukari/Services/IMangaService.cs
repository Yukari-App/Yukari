using System;
using System.Threading.Tasks;
using Yukari.Models;

namespace Yukari.Services
{
    internal interface IMangaService
    {
        Task<Manga?> GetMangaAsync(Guid id);
    }
}
