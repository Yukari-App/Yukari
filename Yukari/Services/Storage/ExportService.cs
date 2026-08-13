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
using Yukari.Services.UI;

namespace Yukari.Services.Storage;

internal class ExportService : IExportService
{
    private readonly IComicService _comicService;
    private readonly IImageCacheService _imageCacheService;

    public ExportService(IComicService comicService, IImageCacheService imageCacheService)
    {
        _comicService = comicService;
        _imageCacheService = imageCacheService;
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

                var ext = PageUriHelper.GetFileExtension(pages[i].ImageUrl) ?? ".jpg";
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
}
