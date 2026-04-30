using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Models.DTO;

namespace Yukari.Services.Storage;

public interface IDownloadService
{
    Task<string?> DownloadComicCoverAsync(string? imageUrl, ContentKey comicKey);

    Task CleanupUnfavoriteComicsAsync(IReadOnlyList<ContentKey> unfavoriteComics);
}
