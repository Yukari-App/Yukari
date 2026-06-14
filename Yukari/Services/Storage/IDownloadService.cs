using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Yukari.Models;
using Yukari.Models.Common;
using Yukari.Models.DTO;

namespace Yukari.Services.Storage;

public interface IDownloadService
{
    event Action<IReadOnlyList<DownloadItem>>? DownloadsChanged;

    DownloadItem EnqueueChapterDownload(
        ContentKey comicKey,
        ContentKey chapterKey,
        string comicTitle,
        string chapterTitle,
        Func<CancellationToken, Task<Result<IReadOnlyList<ChapterPageModel>>>> pageProvider
    );
    IReadOnlyList<DownloadItem> GetAllDownloads();
    DownloadItem? GetDownload(ContentKey comicKey, ContentKey chapterKey);
    void ClearFinishedDownloads();
    Task DeleteComicDataAndDownloadsAsync(ContentKey comicKey);
    Task DeleteChapterDownloadAsync(ContentKey comicKey, ContentKey chapterKey);
    Task CleanupUnfavoriteComicsAsync(IReadOnlyList<ContentKey> unfavoriteComics);

    Task<string?> DownloadComicCoverAsync(string? imageUrl, ContentKey comicKey);
}
