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
        LocalChapterFormat format,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<ChapterPageModel>> GetPagesAsync(
        string chapterPath,
        LocalChapterFormat format,
        CancellationToken ct = default
    );
}
