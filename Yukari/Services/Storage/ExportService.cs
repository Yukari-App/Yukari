using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Yukari.Enums;
using Yukari.Helpers;
using Yukari.Models.Common;
using Yukari.Models.DTO;
using Yukari.Services.Comics;
using Yukari.Services.Sources;
using Yukari.Services.UI;

namespace Yukari.Services.Storage;

internal class ExportService : IExportService
{
    private readonly IComicService _comicService;
    private readonly IImageCacheService _imageCacheService;
    private readonly ISourceService? _sourceService;

    public ExportService(
        IComicService comicService,
        IImageCacheService imageCacheService,
        ISourceService? sourceService = null
    )
    {
        _comicService = comicService;
        _imageCacheService = imageCacheService;
        _sourceService = sourceService;
    }

    public async Task<Result> ExportChapterAsync(
        ContentKey comicKey,
        ContentKey chapterKey,
        ExportFormat format,
        string destinationPath,
        IProgress<(int, int)>? progress,
        CancellationToken ct = default
    )
    {
        var pagesResult = await _comicService.GetChapterPagesAsync(comicKey, chapterKey, ct: ct);
        if (!pagesResult.IsSuccess)
            return Result.Failure(pagesResult.Error!, pagesResult.ErrorTitle!);

        var pages = pagesResult.Value!;
        try
        {
            if (format != ExportFormat.Cbz)
                Directory.CreateDirectory(destinationPath);
            else
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            using var archive =
                format == ExportFormat.Cbz
                    ? ZipFile.Open(destinationPath, ZipArchiveMode.Create)
                    : null;

            for (int i = 0; i < pages.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var bytes = await _imageCacheService.GetImageBytesAsync(pages[i].ImageUrl);
                if (bytes == null)
                    return Result.Failure($"Failed to retrieve image for page {i + 1}.");

                var ext = ImageUriHelper.GetFileExtension(pages[i].ImageUrl) ?? ".jpg";
                var fileName = $"{i + 1:D3}{ext}";

                if (archive != null)
                {
                    var entry = archive.CreateEntry(fileName);
                    using var entryStream = entry.Open();
                    await entryStream.WriteAsync(bytes, ct);
                }
                else
                {
                    await File.WriteAllBytesAsync(
                        Path.Combine(destinationPath, fileName),
                        bytes,
                        ct
                    );
                }

                progress?.Report((i + 1, pages.Count));
            }

            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            return Result.Cancelled();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> ExportLibraryAsync(
        string destinationPath,
        bool includeDownloads,
        bool includePlugins,
        IProgress<(int Current, int Total)>? progress = null,
        CancellationToken ct = default
    )
    {
        try
        {
            await Task.Run(
                () =>
                    LibraryBackupHelper.ExportArchiveAsync(
                        AppDataHelper.GetAppDataPath(),
                        destinationPath,
                        includeDownloads,
                        includePlugins,
                        progress,
                        ct
                    ),
                ct
            );

            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            return Result.Cancelled();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> ImportLibraryAsync(
        string archivePath,
        LibraryImportMode mode,
        IProgress<(int Current, int Total)>? progress = null,
        CancellationToken ct = default
    )
    {
        try
        {
            var pendingRestart = await Task.Run(
                () =>
                    LibraryBackupHelper.ImportArchiveAsync(
                        AppDataHelper.GetAppDataPath(),
                        archivePath,
                        mode,
                        progress,
                        ct
                    ),
                ct
            );

            _imageCacheService.ClearCache();
            if (_sourceService != null)
                await _sourceService.ApplySettingsToAllLoadedSourcesAsync(ct);

            return pendingRestart ? Result.PendingRestart() : Result.Success();
        }
        catch (OperationCanceledException)
        {
            return Result.Cancelled();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}
