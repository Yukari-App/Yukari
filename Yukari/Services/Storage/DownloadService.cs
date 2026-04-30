using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Yukari.Helpers;
using Yukari.Models.DTO;

namespace Yukari.Services.Storage;

internal class DownloadService : IDownloadService
{
    private readonly HttpClient _httpClient = new();

    public DownloadService() => _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Yukari");

    public async Task<string?> DownloadComicCoverAsync(string? imageUrl, ContentKey comicKey)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        var destFolder = AppDataHelper.GetComicDataPath(comicKey);
        var ext = Path.GetExtension(imageUrl);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".jpg";
        var destFile = Path.Combine(destFolder, "cover" + ext);

        try
        {
            using var resp = await _httpClient.GetAsync(imageUrl);
            resp.EnsureSuccessStatusCode();
            await using var stream = await resp.Content.ReadAsStreamAsync();
            await using var fs = new FileStream(
                destFile,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None
            );
            await stream.CopyToAsync(fs);
            return destFile;
        }
        catch
        {
            return imageUrl;
        }
    }

    public async Task CleanupUnfavoriteComicsAsync(IReadOnlyList<ContentKey> unfavoriteComics)
    {
        foreach (var comicKey in unfavoriteComics)
        {
            var comicDataPath = AppDataHelper.GetComicDataPath(comicKey);
            if (Directory.Exists(comicDataPath))
            {
                try
                {
                    await Task.Run(() => Directory.Delete(comicDataPath, true));
                }
                catch
                {
                    // TO-DO: Log error
                }
            }
        }
    }
}
