using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Yukari.Enums;
using Yukari.Models.DTO;
using Yukari.Services.Storage;

namespace Yukari.Helpers;

public static class LibraryBackupHelper
{
    public const string ManifestEntryName = "manifest.json";
    public const string DatabaseEntryName = "yukari.db";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly HashSet<string> PreCompressedExtensions = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".avif",
        ".gif",
    };

    public static string? RemapAppDataPath(
        string? path,
        string? originalAppDataPath,
        string currentAppDataPath
    )
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        if (
            path.StartsWith(ImageUriHelper.SourceScheme, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(ImageUriHelper.ZipScheme, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("ms-appx:///", StringComparison.OrdinalIgnoreCase)
        )
        {
            return path;
        }

        var normalizedPath = NormalizePath(path);
        var normalizedCurrent = NormalizePath(currentAppDataPath).TrimEnd('\\');

        if (!string.IsNullOrWhiteSpace(originalAppDataPath))
        {
            var prefix = NormalizePath(originalAppDataPath).TrimEnd('\\') + "\\";
            if (normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return Path.Combine(normalizedCurrent, normalizedPath[prefix.Length..]);
        }

        foreach (var marker in (ReadOnlySpan<string>)[@"\Yukari\Data\", @"\Yukari\Plugins\"])
        {
            var index = normalizedPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
                return Path.Combine(
                    normalizedCurrent,
                    normalizedPath[(index + @"\Yukari\".Length)..]
                );
        }

        return path;
    }

    public static async Task ExportArchiveAsync(
        string appDataPath,
        string destinationZipPath,
        bool includeDownloads,
        bool includePlugins,
        IProgress<(int Current, int Total)>? progress = null,
        CancellationToken ct = default
    )
    {
        var sourceDbPath = Path.Combine(appDataPath, "Data", DatabaseEntryName);
        await EnsureDatabaseMigratedAsync(sourceDbPath);

        var tempDir = Path.Combine(Path.GetTempPath(), $"YukariExport_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempZip = destinationZipPath + ".tmp";

        try
        {
            ct.ThrowIfCancellationRequested();

            var tempDbPath = Path.Combine(tempDir, DatabaseEntryName);
            await CreateDatabaseSnapshotAsync(sourceDbPath, tempDbPath);

            var (dbVersion, favoriteComics, comicSources) = await PrepareExportDatabaseAsync(
                tempDbPath,
                includeDownloads,
                includePlugins
            );

            var filesToPack = CollectFilesToExport(
                appDataPath,
                favoriteComics,
                comicSources,
                includeDownloads,
                includePlugins
            );

            int totalSteps = 2 + filesToPack.Count;
            int currentStep = 0;

            var manifest = new LibraryExportManifest(
                AppInfoHelper.Version,
                dbVersion,
                DateTime.UtcNow,
                includeDownloads,
                includePlugins,
                appDataPath
            );

            var destDir = Path.GetDirectoryName(destinationZipPath);
            if (!string.IsNullOrWhiteSpace(destDir))
                Directory.CreateDirectory(destDir);

            if (File.Exists(tempZip))
                File.Delete(tempZip);

            using (var archive = ZipFile.Open(tempZip, ZipArchiveMode.Create))
            {
                ct.ThrowIfCancellationRequested();
                var manifestEntry = archive.CreateEntry(
                    ManifestEntryName,
                    CompressionLevel.Optimal
                );
                await using (var manifestStream = manifestEntry.Open())
                    await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, ct);
                progress?.Report((++currentStep, totalSteps));

                ct.ThrowIfCancellationRequested();
                archive.CreateEntryFromFile(
                    tempDbPath,
                    DatabaseEntryName,
                    CompressionLevel.Optimal
                );
                progress?.Report((++currentStep, totalSteps));

                foreach (var (sourceFile, entryName) in filesToPack)
                {
                    ct.ThrowIfCancellationRequested();
                    var compression = PreCompressedExtensions.Contains(
                        Path.GetExtension(sourceFile)
                    )
                        ? CompressionLevel.NoCompression
                        : CompressionLevel.Optimal;
                    archive.CreateEntryFromFile(sourceFile, entryName, compression);
                    progress?.Report((++currentStep, totalSteps));
                }
            }

            ct.ThrowIfCancellationRequested();
            File.Move(tempZip, destinationZipPath, overwrite: true);
        }
        catch
        {
            try
            {
                if (File.Exists(tempZip))
                    File.Delete(tempZip);
            }
            catch { }
            throw;
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    public static async Task<bool> ImportArchiveAsync(
        string appDataPath,
        string archivePath,
        LibraryImportMode mode,
        IProgress<(int Current, int Total)>? progress = null,
        CancellationToken ct = default
    )
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Backup archive file not found.", archivePath);

        var tempDir = Path.Combine(Path.GetTempPath(), $"YukariImport_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            ct.ThrowIfCancellationRequested();

            using var archive = ZipFile.OpenRead(archivePath);

            var manifestEntry =
                archive.Entries.FirstOrDefault(e =>
                    string.Equals(e.FullName, ManifestEntryName, StringComparison.OrdinalIgnoreCase)
                )
                ?? throw new InvalidDataException("Invalid backup archive: missing manifest.json.");

            var dbEntry =
                archive.Entries.FirstOrDefault(e =>
                    string.Equals(e.FullName, DatabaseEntryName, StringComparison.OrdinalIgnoreCase)
                ) ?? throw new InvalidDataException("Invalid backup archive: missing yukari.db.");

            LibraryExportManifest manifest;
            await using (var manifestStream = manifestEntry.Open())
            {
                manifest =
                    await JsonSerializer.DeserializeAsync<LibraryExportManifest>(
                        manifestStream,
                        JsonOptions,
                        ct
                    ) ?? throw new InvalidDataException("Invalid backup manifest.");
            }

            var fileEntries = archive
                .Entries.Where(e =>
                    !string.IsNullOrEmpty(e.Name) && IsAllowedArchiveFileEntry(e.FullName)
                )
                .ToList();

            int totalSteps = 2 + fileEntries.Count;
            int currentStep = 0;

            var importedDbPath = Path.Combine(tempDir, DatabaseEntryName);
            dbEntry.ExtractToFile(importedDbPath, overwrite: true);

            var targetDbPath = Path.Combine(appDataPath, "Data", DatabaseEntryName);
            await EnsureDatabaseMigratedAsync(importedDbPath);
            await EnsureDatabaseMigratedAsync(targetDbPath);

            var skippedPluginEntries =
                mode == LibraryImportMode.Merge && manifest.IncludesPlugins
                    ? await FilterOutDowngradedPluginsAsync(targetDbPath, importedDbPath)
                    : [];

            progress?.Report((++currentStep, totalSteps));

            var pendingRestart = false;
            var pendingPluginUpdates = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase
            );
            var fullAppDataRoot =
                Path.GetFullPath(appDataPath).TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            foreach (var entry in fileEntries)
            {
                ct.ThrowIfCancellationRequested();
                var normalizedEntry = entry.FullName.Replace('\\', '/');

                if (
                    normalizedEntry.StartsWith("Plugins/", StringComparison.OrdinalIgnoreCase)
                    && (!manifest.IncludesPlugins || skippedPluginEntries.Contains(normalizedEntry))
                )
                {
                    progress?.Report((++currentStep, totalSteps));
                    continue;
                }

                if (
                    !manifest.IncludesDownloads
                    && normalizedEntry.StartsWith("Data/", StringComparison.OrdinalIgnoreCase)
                    && normalizedEntry.Split('/', StringSplitOptions.RemoveEmptyEntries).Length > 4
                )
                {
                    progress?.Report((++currentStep, totalSteps));
                    continue;
                }

                var destPath = Path.GetFullPath(
                    Path.Combine(
                        appDataPath,
                        normalizedEntry.Replace('/', Path.DirectorySeparatorChar)
                    )
                );

                if (!destPath.StartsWith(fullAppDataRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Invalid entry path in backup archive.");

                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

                if (
                    normalizedEntry.StartsWith("Plugins/", StringComparison.OrdinalIgnoreCase)
                    && destPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                )
                {
                    try
                    {
                        entry.ExtractToFile(destPath, overwrite: true);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        var pendingDir = Path.Combine(appDataPath, "Plugins", "Pending");
                        Directory.CreateDirectory(pendingDir);

                        var dllFileName = Path.GetFileName(destPath);
                        var stagingPath = Path.Combine(pendingDir, dllFileName);
                        entry.ExtractToFile(stagingPath, overwrite: true);

                        pendingPluginUpdates[dllFileName] = stagingPath;
                        pendingRestart = true;
                    }
                }
                else
                {
                    entry.ExtractToFile(destPath, overwrite: true);
                }

                progress?.Report((++currentStep, totalSteps));
            }

            await RemapImportedDatabasePathsAsync(
                importedDbPath,
                manifest.OriginalAppDataPath,
                appDataPath,
                pendingPluginUpdates
            );

            var (importedComics, removedPluginLocked) = await ApplyDatabaseImportAsync(
                targetDbPath,
                importedDbPath,
                mode,
                manifest.IncludesPlugins
            );

            if (removedPluginLocked)
                pendingRestart = true;

            if (mode == LibraryImportMode.Replace)
            {
                CleanupReplacedOnlineComicFolders(
                    appDataPath,
                    importedComics,
                    manifest.IncludesDownloads
                );
            }

            progress?.Report((++currentStep, totalSteps));
            return pendingRestart;
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    private static bool IsAllowedArchiveFileEntry(string fullName)
    {
        var segments = fullName
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return false;

        if (string.Equals(segments[0], "Data", StringComparison.OrdinalIgnoreCase))
        {
            return segments.Length >= 4
                && !string.Equals(
                    segments[1],
                    LocalComicHelper.SourceName,
                    StringComparison.OrdinalIgnoreCase
                );
        }

        if (string.Equals(segments[0], "Plugins", StringComparison.OrdinalIgnoreCase))
        {
            return (
                    segments.Length == 2
                    && segments[1].EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                )
                || (
                    segments.Length == 3
                    && string.Equals(segments[1], "Images", StringComparison.OrdinalIgnoreCase)
                );
        }

        return false;
    }

    private static async Task<HashSet<string>> FilterOutDowngradedPluginsAsync(
        string targetDbPath,
        string importedDbPath
    )
    {
        var skippedEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var targetConn = new SqliteConnection($"Data Source={targetDbPath}");
        await targetConn.OpenAsync();
        var existingSources = (
            await targetConn.QueryAsync<(string Name, string Version)>(
                "SELECT Name, Version FROM ComicSources WHERE PendingRemoval = 0;"
            )
        ).ToDictionary(s => s.Name, s => s.Version, StringComparer.OrdinalIgnoreCase);

        if (existingSources.Count == 0)
            return skippedEntries;

        using var importedConn = new SqliteConnection(
            $"Data Source={importedDbPath};Pooling=False"
        );
        await importedConn.OpenAsync();
        var importedSources = await importedConn.QueryAsync<(
            string Name,
            string Version,
            string DllPath,
            string? LogoUrl
        )>("SELECT Name, Version, DllPath, LogoUrl FROM ComicSources;");

        foreach (var imported in importedSources)
        {
            if (
                !existingSources.TryGetValue(imported.Name, out var existingVerStr)
                || !Version.TryParse(existingVerStr.Trim().TrimStart('v', 'V'), out var existingVer)
                || !Version.TryParse(
                    imported.Version.Trim().TrimStart('v', 'V'),
                    out var importedVer
                )
                || existingVer <= importedVer
            )
            {
                continue;
            }

            await importedConn.ExecuteAsync(
                "DELETE FROM ComicSources WHERE Name = @Name;",
                new { imported.Name }
            );

            var dllFileName = Path.GetFileName(NormalizePath(imported.DllPath));
            if (!string.IsNullOrEmpty(dllFileName))
                skippedEntries.Add($"Plugins/{dllFileName}");

            if (!string.IsNullOrWhiteSpace(imported.LogoUrl))
            {
                var logoFileName = Path.GetFileName(NormalizePath(imported.LogoUrl));
                if (!string.IsNullOrEmpty(logoFileName))
                    skippedEntries.Add($"Plugins/Images/{logoFileName}");
            }
        }

        return skippedEntries;
    }

    private static async Task EnsureDatabaseMigratedAsync(string dbPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var migrator = new DatabaseMigrator(
            NullLogger<DatabaseMigrator>.Instance,
            $"Data Source={dbPath};Pooling=False"
        );
        await migrator.MigrateAsync();
    }

    private static async Task CreateDatabaseSnapshotAsync(string sourceDbPath, string tempDbPath)
    {
        using var sourceConnection = new SqliteConnection($"Data Source={sourceDbPath}");
        await sourceConnection.OpenAsync();

        using var destConnection = new SqliteConnection($"Data Source={tempDbPath};Pooling=False");
        await destConnection.OpenAsync();

        sourceConnection.BackupDatabase(destConnection);
    }

    private static async Task<(
        int DbVersion,
        List<ContentKey> FavoriteComics,
        List<(string Name, string DllPath, string? LogoUrl)> ComicSources
    )> PrepareExportDatabaseAsync(string tempDbPath, bool includeDownloads, bool includePlugins)
    {
        using var connection = new SqliteConnection($"Data Source={tempDbPath};Pooling=False");
        await connection.OpenAsync();
        await connection.ExecuteAsync("PRAGMA foreign_keys = ON;");

        int dbVersion = await connection.QueryFirstAsync<int>("PRAGMA user_version;");

        using (var transaction = await connection.BeginTransactionAsync())
        {
            // Deleting from Comics cascades to Chapters, ChapterPages, and ComicCollections
            await connection.ExecuteAsync(
                """
                DELETE FROM Comics
                WHERE Source = 'Local'
                   OR NOT EXISTS (
                       SELECT 1 FROM ComicUserData u
                       WHERE u.ComicId = Comics.Id AND u.Source = Comics.Source AND u.IsFavorite = 1
                   );

                DELETE FROM ComicUserData
                WHERE NOT EXISTS (SELECT 1 FROM Comics c WHERE c.Id = ComicUserData.ComicId AND c.Source = ComicUserData.Source);

                DELETE FROM ComicReadingProgress
                WHERE NOT EXISTS (SELECT 1 FROM Comics c WHERE c.Id = ComicReadingProgress.ComicId AND c.Source = ComicReadingProgress.Source);

                DELETE FROM ChapterUserData
                WHERE NOT EXISTS (SELECT 1 FROM Comics c WHERE c.Id = ChapterUserData.ComicId AND c.Source = ChapterUserData.Source);
                """,
                transaction: transaction
            );

            if (!includeDownloads)
            {
                await connection.ExecuteAsync(
                    "DELETE FROM ChapterPages; UPDATE ChapterUserData SET IsDownloaded = 0;",
                    transaction: transaction
                );
            }

            await connection.ExecuteAsync(
                includePlugins
                    ? "DELETE FROM ComicSources WHERE PendingRemoval = 1; UPDATE ComicSources SET PendingRemoval = 0, PendingUpdatePath = NULL;"
                    : "DELETE FROM ComicSources;",
                transaction: transaction
            );

            await transaction.CommitAsync();
        }

        var favoriteComics = (
            await connection.QueryAsync<ContentKey>("SELECT Id, Source FROM Comics;")
        ).ToList();

        var comicSources = includePlugins
            ? (
                await connection.QueryAsync<(string Name, string DllPath, string? LogoUrl)>(
                    "SELECT Name, DllPath, LogoUrl FROM ComicSources;"
                )
            ).ToList()
            : [];

        await connection.ExecuteAsync("VACUUM;");

        return (dbVersion, favoriteComics, comicSources);
    }

    private static List<(string SourceFile, string EntryName)> CollectFilesToExport(
        string appDataPath,
        IReadOnlyList<ContentKey> favoriteComics,
        IReadOnlyList<(string Name, string DllPath, string? LogoUrl)> comicSources,
        bool includeDownloads,
        bool includePlugins
    )
    {
        var files = new List<(string SourceFile, string EntryName)>();
        var seenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var comic in favoriteComics)
        {
            var comicDir = Path.Combine(appDataPath, "Data", comic.Source, comic.Id);
            if (!Directory.Exists(comicDir))
                continue;

            var searchOption = includeDownloads
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            foreach (var file in Directory.GetFiles(comicDir, "*", searchOption))
            {
                var relative = Path.GetRelativePath(appDataPath, file).Replace('\\', '/');
                if (seenEntries.Add(relative))
                    files.Add((file, relative));
            }
        }

        if (includePlugins)
        {
            var pluginsDir = Path.Combine(appDataPath, "Plugins");
            var pluginImagesDir = Path.Combine(pluginsDir, "Images");

            void AddPluginFile(string? filePath, string fallbackDir, string entryFolder)
            {
                if (string.IsNullOrWhiteSpace(filePath))
                    return;

                var fileName = Path.GetFileName(NormalizePath(filePath));
                var candidatePath = File.Exists(filePath)
                    ? filePath
                    : Path.Combine(fallbackDir, fileName);

                if (File.Exists(candidatePath))
                {
                    var entryName = $"{entryFolder}/{fileName}";
                    if (seenEntries.Add(entryName))
                        files.Add((candidatePath, entryName));
                }
            }

            foreach (var source in comicSources)
            {
                AddPluginFile(source.DllPath, pluginsDir, "Plugins");

                if (
                    source.LogoUrl?.StartsWith(
                        ImageUriHelper.SourceScheme,
                        StringComparison.OrdinalIgnoreCase
                    ) != true
                )
                {
                    AddPluginFile(source.LogoUrl, pluginImagesDir, "Plugins/Images");
                }
            }
        }

        return files;
    }

    private static async Task RemapImportedDatabasePathsAsync(
        string importedDbPath,
        string? originalAppDataPath,
        string currentAppDataPath,
        IReadOnlyDictionary<string, string> pendingPluginUpdates
    )
    {
        using var connection = new SqliteConnection($"Data Source={importedDbPath};Pooling=False");
        await connection.OpenAsync();

        var pluginsDir = Path.Combine(NormalizePath(currentAppDataPath).TrimEnd('\\'), "Plugins");
        connection.CreateFunction<string?, string?>(
            "remap_path",
            p => RemapAppDataPath(p, originalAppDataPath, currentAppDataPath)
        );
        connection.CreateFunction<string, string>(
            "remap_dll",
            p => Path.Combine(pluginsDir, Path.GetFileName(NormalizePath(p)))
        );
        connection.CreateFunction<string, string?>(
            "pending_update",
            p => pendingPluginUpdates.GetValueOrDefault(Path.GetFileName(NormalizePath(p)))
        );

        await connection.ExecuteAsync(
            """
            UPDATE Comics SET CoverImageUrl = remap_path(CoverImageUrl) WHERE CoverImageUrl IS NOT NULL;
            UPDATE ChapterPages SET ImageUrl = COALESCE(remap_path(ImageUrl), ImageUrl);
            UPDATE ComicSources
            SET DllPath = remap_dll(DllPath),
                LogoUrl = remap_path(LogoUrl),
                PendingRemoval = 0,
                PendingUpdatePath = pending_update(DllPath);
            """
        );
    }

    private static void CleanupReplacedOnlineComicFolders(
        string appDataPath,
        IReadOnlySet<ContentKey> importedComics,
        bool includesDownloads
    )
    {
        var dataDir = Path.Combine(appDataPath, "Data");
        if (!Directory.Exists(dataDir))
            return;

        var importedSources = importedComics
            .Select(c => c.Source)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var sourceDir in Directory.GetDirectories(dataDir))
        {
            var sourceName = Path.GetFileName(sourceDir);
            if (
                string.Equals(
                    sourceName,
                    LocalComicHelper.SourceName,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                continue;
            }

            if (!importedSources.Contains(sourceName))
            {
                TryDeleteDirectory(sourceDir);
                continue;
            }

            foreach (var comicDir in Directory.GetDirectories(sourceDir))
            {
                var key = new ContentKey(Path.GetFileName(comicDir), sourceName);
                if (!importedComics.Contains(key))
                {
                    TryDeleteDirectory(comicDir);
                }
                else if (!includesDownloads)
                {
                    foreach (var chapterDir in Directory.GetDirectories(comicDir))
                        TryDeleteDirectory(chapterDir);
                }
            }
        }
    }

    private static async Task<(
        HashSet<ContentKey> ImportedComics,
        bool RemovedPluginLocked
    )> ApplyDatabaseImportAsync(
        string targetDbPath,
        string importedDbPath,
        LibraryImportMode mode,
        bool includesPlugins
    )
    {
        using var connection = new SqliteConnection($"Data Source={targetDbPath}");
        await connection.OpenAsync();
        await connection.ExecuteAsync("PRAGMA foreign_keys = ON;");
        await connection.ExecuteAsync(
            "ATTACH DATABASE @ImportedDbPath AS imported;",
            new { ImportedDbPath = importedDbPath }
        );

        var importedComics = new HashSet<ContentKey>();
        var removedSources = new List<(string Name, string DllPath, string? LogoUrl)>();
        var removedPluginLocked = false;

        try
        {
            var importedKeys = await connection.QueryAsync<ContentKey>(
                "SELECT Id, Source FROM imported.Comics WHERE Source != 'Local';"
            );
            foreach (var key in importedKeys)
                importedComics.Add(key);

            using (var transaction = await connection.BeginTransactionAsync())
            {
                if (mode == LibraryImportMode.Replace)
                {
                    // Deleting from main.Comics cascades to Chapters, ChapterPages, and ComicCollections
                    await connection.ExecuteAsync(
                        """
                        DELETE FROM main.Comics WHERE Source != 'Local';
                        DELETE FROM main.ComicUserData WHERE Source != 'Local';
                        DELETE FROM main.ComicReadingProgress WHERE Source != 'Local';
                        DELETE FROM main.ChapterUserData WHERE Source != 'Local';
                        DELETE FROM main.Collections
                        WHERE NOT EXISTS (SELECT 1 FROM main.ComicCollections cc WHERE cc.CollectionId = main.Collections.Id);
                        """,
                        transaction: transaction
                    );

                    if (includesPlugins)
                    {
                        var existingSources = await connection.QueryAsync<(
                            string Name,
                            string DllPath,
                            string? LogoUrl
                        )>(
                            "SELECT Name, DllPath, LogoUrl FROM main.ComicSources;",
                            transaction: transaction
                        );

                        var importedNames = (
                            await connection.QueryAsync<string>(
                                "SELECT Name FROM imported.ComicSources;",
                                transaction: transaction
                            )
                        ).ToHashSet(StringComparer.OrdinalIgnoreCase);

                        removedSources.AddRange(
                            existingSources.Where(s => !importedNames.Contains(s.Name))
                        );
                    }
                }

                await connection.ExecuteAsync(
                    """
                    INSERT INTO main.Comics SELECT * FROM imported.Comics WHERE Source != 'Local'
                    ON CONFLICT(Id, Source) DO UPDATE SET
                        ComicUrl = excluded.ComicUrl, Title = excluded.Title, Author = excluded.Author,
                        Description = excluded.Description, Status = excluded.Status, Tags = excluded.Tags,
                        Year = excluded.Year, CoverImageUrl = COALESCE(excluded.CoverImageUrl, main.Comics.CoverImageUrl),
                        Langs = excluded.Langs, LastUpdate = COALESCE(excluded.LastUpdate, main.Comics.LastUpdate),
                        IsAvailable = excluded.IsAvailable;

                    INSERT INTO main.ComicReadingProgress SELECT * FROM imported.ComicReadingProgress WHERE Source != 'Local'
                    ON CONFLICT(ComicId, Source, Language) DO UPDATE SET
                        LastChapterId = CASE
                            WHEN excluded.LastChapterId IS NULL THEN main.ComicReadingProgress.LastChapterId
                            WHEN main.ComicReadingProgress.LastChapterId IS NULL THEN excluded.LastChapterId
                            WHEN (SELECT cud.LastReadAt FROM main.ComicUserData cud WHERE cud.ComicId = excluded.ComicId AND cud.Source = excluded.Source) IS NULL THEN excluded.LastChapterId
                            WHEN (SELECT icud.LastReadAt FROM imported.ComicUserData icud WHERE icud.ComicId = excluded.ComicId AND icud.Source = excluded.Source)
                               >= (SELECT cud.LastReadAt FROM main.ComicUserData cud WHERE cud.ComicId = excluded.ComicId AND cud.Source = excluded.Source) THEN excluded.LastChapterId
                            ELSE main.ComicReadingProgress.LastChapterId
                        END;

                    INSERT INTO main.ComicUserData SELECT * FROM imported.ComicUserData WHERE Source != 'Local'
                    ON CONFLICT(ComicId, Source) DO UPDATE SET
                        IsFavorite = MAX(main.ComicUserData.IsFavorite, excluded.IsFavorite),
                        LastSelectedLang = COALESCE(excluded.LastSelectedLang, main.ComicUserData.LastSelectedLang),
                        LastReadAt = CASE
                            WHEN main.ComicUserData.LastReadAt IS NULL THEN excluded.LastReadAt
                            WHEN excluded.LastReadAt IS NULL THEN main.ComicUserData.LastReadAt
                            WHEN excluded.LastReadAt > main.ComicUserData.LastReadAt THEN excluded.LastReadAt
                            ELSE main.ComicUserData.LastReadAt
                        END;

                    INSERT INTO main.Chapters SELECT * FROM imported.Chapters ic
                    WHERE ic.Source != 'Local' AND EXISTS (SELECT 1 FROM main.Comics mc WHERE mc.Id = ic.ComicId AND mc.Source = ic.Source)
                    ON CONFLICT(Id, ComicId, Source) DO UPDATE SET
                        Title = excluded.Title, Number = excluded.Number, Volume = excluded.Volume,
                        Language = excluded.Language, Groups = excluded.Groups, LastUpdate = excluded.LastUpdate,
                        Pages = COALESCE(excluded.Pages, main.Chapters.Pages), SortOrder = excluded.SortOrder,
                        IsAvailable = excluded.IsAvailable;

                    INSERT INTO main.ChapterUserData SELECT * FROM imported.ChapterUserData WHERE Source != 'Local'
                    ON CONFLICT(Id, ComicId, Source) DO UPDATE SET
                        LastPageRead = CASE
                            WHEN main.ChapterUserData.LastPageRead IS NULL THEN excluded.LastPageRead
                            WHEN excluded.LastPageRead IS NULL THEN main.ChapterUserData.LastPageRead
                            ELSE MAX(main.ChapterUserData.LastPageRead, excluded.LastPageRead)
                        END,
                        IsDownloaded = MAX(COALESCE(main.ChapterUserData.IsDownloaded, 0), COALESCE(excluded.IsDownloaded, 0)),
                        IsRead = MAX(COALESCE(main.ChapterUserData.IsRead, 0), COALESCE(excluded.IsRead, 0));

                    INSERT INTO main.ChapterPages SELECT * FROM imported.ChapterPages ip
                    WHERE ip.Source != 'Local' AND EXISTS (SELECT 1 FROM main.Chapters mc WHERE mc.Id = ip.ChapterId AND mc.ComicId = ip.ComicId AND mc.Source = ip.Source)
                    ON CONFLICT(ChapterId, ComicId, Source, Number) DO UPDATE SET ImageUrl = excluded.ImageUrl;

                    INSERT OR IGNORE INTO main.Collections (Name) SELECT Name FROM imported.Collections;

                    INSERT OR IGNORE INTO main.ComicCollections (ComicId, Source, CollectionId)
                    SELECT icc.ComicId, icc.Source, mc.Id
                    FROM imported.ComicCollections icc
                    INNER JOIN imported.Collections ic ON icc.CollectionId = ic.Id
                    INNER JOIN main.Collections mc ON mc.Name = ic.Name
                    WHERE icc.Source != 'Local' AND EXISTS (SELECT 1 FROM main.Comics c WHERE c.Id = icc.ComicId AND c.Source = icc.Source);
                    """,
                    transaction: transaction
                );

                if (includesPlugins)
                {
                    // SQLite parser requires a WHERE clause before ON CONFLICT in INSERT INTO ... SELECT statements
                    await connection.ExecuteAsync(
                        """
                        INSERT INTO main.ComicSources SELECT * FROM imported.ComicSources WHERE 1 = 1
                        ON CONFLICT(Name) DO UPDATE SET
                            Version = excluded.Version, ReleasesPage = excluded.ReleasesPage,
                            LogoUrl = COALESCE(excluded.LogoUrl, main.ComicSources.LogoUrl),
                            Description = excluded.Description, DllPath = excluded.DllPath,
                            IsEnabled = excluded.IsEnabled, PendingRemoval = 0,
                            PendingUpdatePath = excluded.PendingUpdatePath;
                        """,
                        transaction: transaction
                    );
                }

                await transaction.CommitAsync();
            }

            foreach (var source in removedSources)
            {
                var isLocked = false;
                try
                {
                    if (!string.IsNullOrWhiteSpace(source.DllPath) && File.Exists(source.DllPath))
                        File.Delete(source.DllPath);

                    if (
                        !string.IsNullOrWhiteSpace(source.LogoUrl)
                        && !source.LogoUrl.StartsWith(
                            ImageUriHelper.SourceScheme,
                            StringComparison.OrdinalIgnoreCase
                        )
                        && File.Exists(source.LogoUrl)
                    )
                    {
                        File.Delete(source.LogoUrl);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    isLocked = true;
                    removedPluginLocked = true;
                }

                await connection.ExecuteAsync(
                    isLocked
                        ? "UPDATE main.ComicSources SET PendingRemoval = 1, IsEnabled = 0 WHERE Name = @Name;"
                        : "DELETE FROM main.ComicSources WHERE Name = @Name;",
                    new { source.Name }
                );
            }
        }
        finally
        {
            await connection.ExecuteAsync("DETACH DATABASE imported;");
        }

        try
        {
            await connection.ExecuteAsync("VACUUM;");
        }
        catch
        {
            // Best-effort VACUUM on live database; ignore SQLITE_BUSY if another connection is active
        }

        return (importedComics, removedPluginLocked);
    }

    private static string NormalizePath(string path) => path.Replace('/', '\\');

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch { }
    }
}
