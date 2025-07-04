using System;
using System.Threading.Tasks;
using Yukari.Models;

namespace Yukari.Services
{
    internal interface ISqliteService
    {
        Task<Manga?> GetMangaByIdAsync(Guid id);
    }
}
