using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Yukari.Core.Models.Settings;
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

    Task<
        Result<(IReadOnlyList<SourceSetting> Schema, IReadOnlyDictionary<string, string> Values)>
    > GetSourceSettingsAsync(string sourceName, CancellationToken ct = default);
    Task<Result> SaveSettingsAsync(
        string sourceName,
        IReadOnlyDictionary<string, string> values,
        CancellationToken ct = default
    );
}
