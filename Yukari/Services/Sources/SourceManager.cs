using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Yukari.Helpers;
using Yukari.Models;
using Yukari.Models.Common;
using Yukari.Services.Storage;
using Yukari.Services.UI;

namespace Yukari.Services.Sources;

internal class SourceManager : ResultServiceBase, ISourceManager
{
    private readonly IDataService _dbService;
    private readonly ISourceService _srcService;
    private readonly IDownloadService _dloadService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<SourceManager> _logger;

    public SourceManager(
        IDataService dbService,
        ISourceService srcService,
        IDownloadService dloadService,
        ILocalizationService localizationService,
        ILogger<SourceManager> logger
    )
        : base(logger, localizationService)
    {
        _dbService = dbService;
        _srcService = srcService;
        _dloadService = dloadService;
        _localizationService = localizationService;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<ComicSourceModel>>> GetComicSourcesAsync(
        CancellationToken ct = default
    )
    {
        return await ExecuteAsync(
            async (ct) =>
                Result<IReadOnlyList<ComicSourceModel>>.Success(
                    await _dbService.GetComicSourcesAsync(ct)
                ),
            _localizationService.GetString("ErrorFetchingComicSources"),
            ct
        );
    }

    public async Task<Result> UpsertComicSourceAsync(string pluginPath)
    {
        return await ExecuteAsync(
            async () =>
            {
                var comicSource = await _srcService.GetComicSourceModelFromAssemblyAsync(
                    pluginPath,
                    true
                );
                if (
                    comicSource.Name.Equals(
                        LocalComicHelper.SourceName,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                    return Result.Failure("You can't add a ComicSource named 'Local'");

                try
                {
                    comicSource.DllPath = AppDataHelper.CopyDllToPluginsDirectory(pluginPath);
                    await _dbService.UpsertComicSourceAsync(comicSource);

                    var localLogo = await _dloadService.DownloadPluginLogoAsync(
                        comicSource.LogoUrl,
                        comicSource.Name
                    );

                    if (!string.IsNullOrWhiteSpace(localLogo))
                    {
                        comicSource.LogoUrl = localLogo;
                        await _dbService.UpsertComicSourceAsync(comicSource);
                    }

                    _logger.LogInformation(
                        "Comic source '{SourceName}' (version {Version}) has been added/updated",
                        comicSource.Name,
                        comicSource.Version
                    );
                    return Result.Success();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(
                        ex,
                        "Could not replace DLL for source '{SourceName}' because it's in use. Marking for pending update on next restart.",
                        comicSource.Name
                    );
                    await _dbService.UpdateComicSourcePendingUpdateAsync(
                        comicSource.Name,
                        pluginPath
                    );
                    return Result.PendingRestart();
                }
            },
            _localizationService.GetString("ErrorAddingUpdatingComicSource")
        );
    }

    public async Task<Result> UpdateComicSourceIsEnabledAsync(string sourceName, bool isEnabled)
    {
        return await ExecuteAsync(
            async () =>
            {
                await _dbService.UpdateComicSourceIsEnabledAsync(sourceName, isEnabled);
                if (!isEnabled)
                    await _srcService.UnloadSourceAsync(sourceName);

                _logger.LogInformation(
                    "Comic source '{SourceName}' has been {Status}",
                    sourceName,
                    isEnabled ? "enabled" : "disabled"
                );
                return Result.Success();
            },
            _localizationService.GetString("ErrorUpdatingComicSourceStatus")
        );
    }

    public async Task<Result> RemoveComicSourceAsync(string sourceName)
    {
        return await ExecuteAsync(
            async () =>
            {
                try
                {
                    var comicSource =
                        await _dbService.GetComicSourceDetailsAsync(sourceName)
                        ?? throw new InvalidOperationException(
                            $"The source '{sourceName}' is not registered in the database."
                        );

                    if (
                        !string.IsNullOrWhiteSpace(comicSource.DllPath)
                        && File.Exists(comicSource.DllPath)
                    )
                        File.Delete(comicSource.DllPath);

                    await _dbService.RemoveComicSourceAsync(sourceName);

                    _logger.LogInformation("Comic source '{SourceName}' removed", sourceName);
                    return Result.Success();
                }
                // DLL files loaded by AssemblyLoadContext.Default cannot be deleted while the process is running.
                // Removal and updates are deferred to the next startup via PendingRemoval/PendingUpdatePath,
                // when no plugin assembly has been loaded yet.
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(
                        ex,
                        "Could not delete DLL for source '{SourceName}' because it's in use. Marking for pending removal on next restart.",
                        sourceName
                    );
                    await _dbService.UpdateComicSourcePendingRemovalAsync(sourceName, true);
                    return Result.PendingRestart();
                }
            },
            _localizationService.GetString("ErrorRemovingComicSource")
        );
    }
}
