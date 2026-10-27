using System;
using System.Threading;
using System.Threading.Tasks;
using Yukari.Enums;
using Yukari.Models.Common;
using Yukari.Models.DTO;

namespace Yukari.Services.Storage;

public interface IExportService
{
    Task<Result> ExportChapterAsync(
        ContentKey comicKey,
        ContentKey chapterKey,
        ExportFormat format,
        string destinationPath,
        IProgress<(int Current, int Total)>? progress = null,
        CancellationToken ct = default
    );

    Task<Result> ExportLibraryAsync(
        string destinationPath,
        bool includeDownloads,
        bool includePlugins,
        IProgress<(int Current, int Total)>? progress = null,
        CancellationToken ct = default
    );

    Task<Result> ImportLibraryAsync(
        string archivePath,
        LibraryImportMode mode,
        IProgress<(int Current, int Total)>? progress = null,
        CancellationToken ct = default
    );
}
