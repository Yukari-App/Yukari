using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Yukari.Models;
using Yukari.Models.Common;

namespace Yukari.Services.Sources;

public interface ISourceManager
{
    Task<Result<IReadOnlyList<ComicSourceModel>>> GetComicSourcesAsync(
        CancellationToken ct = default
    );
    Task<Result> UpsertComicSourceAsync(string pluginPath);
    Task<Result> UpdateComicSourceIsEnabledAsync(string sourceName, bool isEnabled);
    Task<Result> RemoveComicSourceAsync(string sourceName);
}
