using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Yukari.Enums;
using Yukari.Models;

namespace Yukari.Services.Sources;

public interface ILocalSourceService
{
    Task<IReadOnlyList<ChapterModel>> ScanChaptersAsync(
        string comicDirectory,
        LocalChaptersFormat format,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<ChapterPageModel>> GetPagesAsync(
        string chapterPath,
        LocalChaptersFormat format,
        CancellationToken ct = default
    );
}
