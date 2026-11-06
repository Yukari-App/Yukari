using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Yukari.Core.Models.Settings;
using Yukari.Core.Sources;

namespace Yukari.Services.Sources;

internal interface ISourceSettingsStore
{
    Task<ISourceSettings> LoadSettingsAsync(
        string sourceName,
        IReadOnlyList<SourceSetting> schema,
        CancellationToken ct = default
    );

    Task<IReadOnlyDictionary<string, string>> LoadRawValuesAsync(
        string sourceName,
        IReadOnlyList<SourceSetting> schema,
        CancellationToken ct = default
    );

    Task SaveSettingsAsync(
        string sourceName,
        IReadOnlyList<SourceSetting> schema,
        IReadOnlyDictionary<string, string> values,
        CancellationToken ct = default
    );
}
