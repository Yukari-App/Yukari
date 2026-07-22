using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Yukari.Enums;
using Yukari.Helpers;
using Yukari.Models;
using Yukari.Models.Common;
using Yukari.Models.DTO;
using Yukari.Services.Sources;

namespace Yukari.Services.Storage;

internal class DownloadService : IDownloadService
{
    private readonly IDataService _dataService;
    private readonly ISourceService _sourceService;
    private readonly ILogger<DownloadService> _logger;

    private readonly Channel<DownloadItem> _downloadQueue = Channel.CreateUnbounded<DownloadItem>();
    private readonly List<DownloadItem> _downloads = new();

    public event Action<IReadOnlyList<DownloadItem>>? DownloadsChanged;

    public DownloadService(
        IDataService dataService,
        ISourceService sourceService,
        ILogger<DownloadService> logger
    )
    {
        _dataService = dataService;
        _sourceService = sourceService;
        _logger = logger;

        _ = ConsumeQueueAsync();
    }

    public DownloadItem EnqueueChapterDownload(
        ContentKey comicKey,
        ContentKey chapterKey,
        string comicTitle,
        string chapterTitle,
        Func<CancellationToken, Task<Result<IReadOnlyList<ChapterPageModel>>>> pageProvider
    )
    {
        var existing = GetDownload(comicKey, chapterKey);
        if (existing != null)
            if (existing.Status is DownloadStatus.Failed or DownloadStatus.Cancelled)
                _downloads.Remove(existing);
            else
                return existing;

        var item = new DownloadItem(comicKey, chapterKey, comicTitle, chapterTitle, pageProvider);
        _downloads.Add(item);
        _downloadQueue.Writer.TryWrite(item);

        NotifyDownloadsChanged();
        return item;
    }

    public IReadOnlyList<DownloadItem> GetAllDownloads() => _downloads.ToList();

    public DownloadItem? GetDownload(ContentKey comicKey, ContentKey chapterKey) =>
        _downloads.FirstOrDefault(d =>
            d.ComicKey.Equals(comicKey) && d.ChapterKey.Equals(chapterKey)
        );

    public void ClearFinishedDownloads()
    {
        var statuses = new[]
        {
            DownloadStatus.Completed,
            DownloadStatus.Cancelled,
            DownloadStatus.Failed,
        };
        var toRemove = _downloads.Where(d => statuses.Contains(d.Status)).ToList();

        if (toRemove.Count == 0)
            return;

        foreach (var item in toRemove)
            _downloads.Remove(item);

        NotifyDownloadsChanged();
    }

    public async Task DeleteComicDataAndDownloadsAsync(ContentKey comicKey)
    {
        var itemsToDelete = _downloads.Where(d => d.ComicKey.Equals(comicKey)).ToList();
        foreach (var item in itemsToDelete)
        {
            if (item.Status is DownloadStatus.Queued or DownloadStatus.Downloading)
                item.Cancel();
            _downloads.Remove(item);
        }

        NotifyDownloadsChanged();
        await _dataService.RemoveAllChapterPagesAsync(comicKey);
        await Task.Run(() => DeleteComicFolder(comicKey));
    }

    public async Task DeleteChapterDownloadAsync(ContentKey comicKey, ContentKey chapterKey)
    {
        var existing = GetDownload(comicKey, chapterKey);
        if (existing?.Status is DownloadStatus.Downloading or DownloadStatus.Queued)
        {
            existing.Cancel();
        }

        await Task.Run(() => DeleteChapterFolder(comicKey, chapterKey));
        await _dataService.RemoveChapterPagesAsync(comicKey, chapterKey);

        if (existing != null)
        {
            _downloads.Remove(existing);
            NotifyDownloadsChanged();
        }
    }

    public async Task CleanupUnfavoriteComicsAsync(IReadOnlyList<ContentKey> unfavoriteComics)
    {
        foreach (var comicKey in unfavoriteComics)
            await Task.Run(() => DeleteComicFolder(comicKey));
    }

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
            return await DownloadImageAsync(imageUrl, destFile);
        }
        catch
        {
            return imageUrl;
        }
    }

    public async Task<string?> DownloadPluginLogoAsync(string? logoUrl, string sourceName)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
            return null;

        var destFolder = AppDataHelper.GetPluginImagesPath();
        var ext = Path.GetExtension(logoUrl);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".png";
        var destFile = Path.Combine(destFolder, sourceName + ext);

        try
        {
            return await DownloadImageAsync(logoUrl, destFile);
        }
        catch
        {
            return logoUrl;
        }
    }

    public async Task<byte[]?> GetImageBytesAsync(string encodedUrl)
    {
        try
        {
            if (!SourceImageUrlHelper.TryDecode(encodedUrl, out var sourceName, out var url))
                return null;
            return await _sourceService.GetImageBytesAsync(sourceName, url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed getting bytes for {encodedUrl}", encodedUrl);
            return null;
        }
    }

    private async Task ConsumeQueueAsync()
    {
        try
        {
            await foreach (var item in _downloadQueue.Reader.ReadAllAsync())
            {
                if (item.Status == DownloadStatus.Cancelled)
                    continue;

                await ProcessDownloadAsync(item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Download queue consumer terminated unexpectedly");
        }
    }

    private async Task ProcessDownloadAsync(DownloadItem item)
    {
        item.Status = DownloadStatus.Downloading;

        var destFolder = AppDataHelper.GetComicChapterDataPath(item.ComicKey, item.ChapterKey.Id);
        try
        {
            var pagesResult = await item.PageProvider(item.Cts.Token);
            if (!pagesResult.IsSuccess)
            {
                if (pagesResult.IsCancelled)
                    throw new OperationCanceledException();

                item.Status = DownloadStatus.Failed;
                _logger.LogWarning(
                    "Failed to get pages for chapter {ChapterKey}: {Error}",
                    item.ChapterKey,
                    pagesResult.Error
                );
                return;
            }

            var pages = pagesResult.Value!;
            foreach (var page in pages)
            {
                item.Cts.Token.ThrowIfCancellationRequested();

                var ext = Path.GetExtension(page.ImageUrl);
                if (string.IsNullOrWhiteSpace(ext))
                    ext = ".jpg";
                var destFile = Path.Combine(destFolder, $"{page.Number}{ext}");

                await DownloadImageAsync(page.ImageUrl, destFile, item.Cts.Token);
                page.ImageUrl = destFile;

                item.Progress = (double)page.Number / pages.Count;
            }

            await _dataService.UpsertChapterPagesAsync(item.ComicKey, item.ChapterKey, pages);
            item.Status = DownloadStatus.Completed;
            _logger.LogInformation("Download completed for chapter {ChapterKey}", item.ChapterKey);
        }
        catch (OperationCanceledException)
        {
            item.Status = DownloadStatus.Cancelled;
            await Task.Run(() => DeleteChapterFolder(item.ComicKey, item.ChapterKey));
            _logger.LogInformation("Download cancelled for chapter {ChapterKey}", item.ChapterKey);
        }
        catch (Exception ex)
        {
            item.Status = DownloadStatus.Failed;
            await Task.Run(() => DeleteChapterFolder(item.ComicKey, item.ChapterKey));
            _logger.LogError(ex, "Download failed for chapter {ChapterKey}", item.ChapterKey);
        }
    }

    private async Task<string> DownloadImageAsync(
        string encodedUrl,
        string destFile,
        CancellationToken ct = default
    )
    {
        if (!SourceImageUrlHelper.TryDecode(encodedUrl, out var sourceName, out var url))
            throw new InvalidOperationException("Invalid Encoded Url");

        byte[]? imageBytes =
            await _sourceService.GetImageBytesAsync(sourceName, url, ct)
            ?? throw new InvalidOperationException("Failed to download image.");

        await File.WriteAllBytesAsync(destFile, imageBytes, ct);
        return destFile;
    }

    private void DeleteComicFolder(ContentKey comicKey)
    {
        try
        {
            AppDataHelper.DeleteComicDataPath(comicKey);
        }
        catch
        {
            _logger.LogError("Failed to delete data for comic {ComicKey}", comicKey);
        }
    }

    private void DeleteChapterFolder(ContentKey comicKey, ContentKey chapterKey)
    {
        try
        {
            AppDataHelper.DeleteComicChapterDataPath(comicKey, chapterKey.Id);
        }
        catch
        {
            _logger.LogError(
                "Failed to delete data for chapter {ChapterKey} of comic {ComicKey}",
                chapterKey,
                comicKey
            );
        }
    }

    private void NotifyDownloadsChanged() => DownloadsChanged?.Invoke(_downloads.ToList());
}
