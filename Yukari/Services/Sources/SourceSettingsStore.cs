using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Yukari.Core.Models.Settings;
using Yukari.Core.Sources;
using Yukari.Services.Security;
using Yukari.Services.Storage;

namespace Yukari.Services.Sources;

internal class SourceSettingsStore : ISourceSettingsStore
{
    private readonly IDataService _dataService;
    private readonly ICredentialProtector _credentialProtector;
    private readonly ILogger<SourceSettingsStore> _logger;

    public SourceSettingsStore(
        IDataService dataService,
        ICredentialProtector credentialProtector,
        ILogger<SourceSettingsStore> logger
    )
    {
        _dataService = dataService;
        _credentialProtector = credentialProtector;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, string>> LoadRawValuesAsync(
        string sourceName,
        IReadOnlyList<SourceSetting> schema,
        CancellationToken ct = default
    )
    {
        var plainSettings = await _dataService.GetComicSourceSettingsAsync(sourceName, ct);
        var result = new Dictionary<string, string>(plainSettings, StringComparer.Ordinal);

        var passwordSettings = schema.OfType<PasswordSetting>().ToList();
        if (passwordSettings.Count > 0)
        {
            var credentials = await _dataService.GetComicSourceCredentialsAsync(sourceName, ct);
            foreach (var pw in passwordSettings)
            {
                if (credentials.TryGetValue(pw.Key, out var cipher))
                {
                    try
                    {
                        result[pw.Key] = _credentialProtector.Decrypt(cipher);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Failed to decrypt credential setting '{Key}' for comic source '{SourceName}'.",
                            pw.Key,
                            sourceName
                        );
                    }
                }
            }
        }

        return result;
    }

    public async Task<ISourceSettings> LoadSettingsAsync(
        string sourceName,
        IReadOnlyList<SourceSetting> schema,
        CancellationToken ct = default
    )
    {
        var rawValues = await LoadRawValuesAsync(sourceName, schema, ct);
        return new SourceSettings(schema, rawValues, _logger);
    }

    public async Task SaveSettingsAsync(
        string sourceName,
        IReadOnlyList<SourceSetting> schema,
        IReadOnlyDictionary<string, string> values,
        CancellationToken ct = default
    )
    {
        var schemaByKey = schema.ToDictionary(s => s.Key, s => s, StringComparer.Ordinal);
        var plainSettings = new Dictionary<string, string>(StringComparer.Ordinal);
        var credentials = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        var passwordSettings = schema.OfType<PasswordSetting>().ToList();
        if (passwordSettings.Count > 0)
        {
            var existingCredentials =
                await _dataService.GetComicSourceCredentialsAsync(sourceName, ct)
                ?? new Dictionary<string, byte[]>();

            foreach (var pw in passwordSettings)
            {
                if (values.TryGetValue(pw.Key, out var newPlain))
                {
                    if (!string.IsNullOrEmpty(newPlain))
                        credentials[pw.Key] = _credentialProtector.Encrypt(newPlain);
                }
                else if (
                    existingCredentials.TryGetValue(pw.Key, out var existingCipher)
                    && existingCipher.Length > 0
                )
                {
                    credentials[pw.Key] = existingCipher;
                }
            }
        }

        foreach (var kvp in values)
        {
            if (!schemaByKey.TryGetValue(kvp.Key, out var setting) || setting is PasswordSetting)
                continue;

            plainSettings[setting.Key] = kvp.Value ?? string.Empty;
        }

        await _dataService.SaveComicSourceSettingsAsync(sourceName, plainSettings);
        await _dataService.SaveComicSourceCredentialsAsync(sourceName, credentials);
    }
}
