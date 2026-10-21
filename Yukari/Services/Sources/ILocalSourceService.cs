using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Yukari.Enums;
using Yukari.Models.DTO;

namespace Yukari.Services.Sources;

public interface ILocalSourceService
{
    Task<IReadOnlyList<LocalChapterScanResult>> ScanChaptersAsync(
        string comicDirectory,
        LocalChapterFormat format,
        CancellationToken ct = default
    );
}
