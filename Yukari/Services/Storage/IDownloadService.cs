using System.Threading.Tasks;
using Yukari.Models.DTO;

namespace Yukari.Services.Storage
{
    public interface IDownloadService
    {
        Task<string?> DownloadComicCoverAsync(string? imageUrl, ContentKey comicKey);
    }
}
