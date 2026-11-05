using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Yukari.Enums;
using Yukari.Helpers;

namespace Yukari.Tests.Helpers;

public class LibraryBackupHelperTests : IDisposable
{
    private readonly string _tempRoot;

    public LibraryBackupHelperTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"YukariBackupTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempRoot))
        {
            try
            {
                Directory.Delete(_tempRoot, true);
            }
            catch { }
        }
    }

    // ────────────────────────────────────────────────────────────────
    // PATH REMAPPING
    // ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(
        @"C:\Users\OldUser\AppData\Roaming\Yukari\Data\MangaDex\comic-1\cover.jpg",
        @"C:\Users\OldUser\AppData\Roaming\Yukari",
        @"C:\Users\NewUser\AppData\Roaming\Yukari",
        @"C:\Users\NewUser\AppData\Roaming\Yukari\Data\MangaDex\comic-1\cover.jpg"
    )]
    [InlineData(
        @"c:/users/olduser/appdata/roaming/yukari/Plugins/Images/MangaDex.png",
        @"C:\Users\OldUser\AppData\Roaming\Yukari\",
        @"D:\Profiles\NewUser\AppData\Roaming\Yukari",
        @"D:\Profiles\NewUser\AppData\Roaming\Yukari\Plugins\Images\MangaDex.png"
    )]
    [InlineData(
        @"C:\SomeOtherRoot\Yukari\Data\SourceA\c1\ch1\1.jpg",
        null,
        @"C:\Users\Bob\AppData\Roaming\Yukari",
        @"C:\Users\Bob\AppData\Roaming\Yukari\Data\SourceA\c1\ch1\1.jpg"
    )]
    [InlineData(
        "source:///MangaDex?url=https%3A%2F%2Fexample.com%2Fcover.jpg",
        @"C:\Users\OldUser\AppData\Roaming\Yukari",
        @"C:\Users\NewUser\AppData\Roaming\Yukari",
        "source:///MangaDex?url=https%3A%2F%2Fexample.com%2Fcover.jpg"
    )]
    [InlineData(
        null,
        @"C:\Users\OldUser\AppData\Roaming\Yukari",
        @"C:\Users\NewUser\AppData\Roaming\Yukari",
        null
    )]
    [InlineData(
        @"C:\Users\Alice\AppData\Roaming\Yukari\Plugins\MySource.dll",
        @"C:\Users\Alice\AppData\Roaming\Yukari",
        @"C:\Users\Bob\AppData\Roaming\Yukari",
        @"C:\Users\Bob\AppData\Roaming\Yukari\Plugins\MySource.dll"
    )]
    public void RemapAppDataPath_ShouldRemapPathsCorrectly(
        string? inputPath,
        string? originalAppData,
        string currentAppData,
        string? expected
    )
    {
        var result = LibraryBackupHelper.RemapAppDataPath(
            inputPath,
            originalAppData,
            currentAppData
        );

        result.Should().Be(expected);
    }

    // ────────────────────────────────────────────────────────────────
    // EXPORT & IMPORT
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExportAndImport_FullBackup_ExcludesLocalAndUnfavorites_AndRemapsPathsForNewUser()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sourceAppData = Path.Combine(
            _tempRoot,
            "Users",
            "Alice",
            "AppData",
            "Roaming",
            "Yukari"
        );
        var targetAppData = Path.Combine(_tempRoot, "Users", "Bob", "AppData", "Roaming", "Yukari");
        var zipPath = Path.Combine(_tempRoot, "backup_full.zip");

        await SeedSampleAppDataAsync(sourceAppData, ct);

        // Act
        await LibraryBackupHelper.ExportArchiveAsync(
            sourceAppData,
            zipPath,
            includeDownloads: true,
            includePlugins: true,
            ct: ct
        );

        var pendingRestart = await LibraryBackupHelper.ImportArchiveAsync(
            targetAppData,
            zipPath,
            LibraryImportMode.Replace,
            ct: ct
        );

        // Assert
        pendingRestart.Should().BeFalse();

        var targetDbPath = Path.Combine(targetAppData, "Data", "yukari.db");
        using var conn = new SqliteConnection($"Data Source={targetDbPath};Pooling=False");
        await conn.OpenAsync(ct);

        var comics = (
            await conn.QueryAsync<(string Id, string Source, string? CoverImageUrl)>(
                "SELECT Id, Source, CoverImageUrl FROM Comics;"
            )
        ).ToList();

        comics.Should().ContainSingle();
        comics[0].Id.Should().Be("c-fav");
        comics[0].Source.Should().Be("MangaDex");
        comics[0]
            .CoverImageUrl.Should()
            .Be(Path.Combine(targetAppData, "Data", "MangaDex", "c-fav", "cover.jpg"));

        var pages = (
            await conn.QueryAsync<(int Number, string ImageUrl)>(
                "SELECT Number, ImageUrl FROM ChapterPages;"
            )
        ).ToList();
        pages.Should().ContainSingle();
        var expectedPagePath = Path.Combine(
            targetAppData,
            "Data",
            "MangaDex",
            "c-fav",
            "ch-1",
            "1.jpg"
        );
        pages[0].ImageUrl.Should().Be(expectedPagePath);
        File.Exists(expectedPagePath).Should().BeTrue();

        var sources = (
            await conn.QueryAsync<(string Name, string DllPath, string? LogoUrl)>(
                "SELECT Name, DllPath, LogoUrl FROM ComicSources;"
            )
        ).ToList();
        sources.Should().ContainSingle();
        sources[0].Name.Should().Be("MangaDex");
        sources[0].DllPath.Should().Be(Path.Combine(targetAppData, "Plugins", "MangaDex.dll"));
        sources[0]
            .LogoUrl.Should()
            .Be(Path.Combine(targetAppData, "Plugins", "Images", "MangaDex.png"));
        File.Exists(sources[0].DllPath).Should().BeTrue();
        File.Exists(sources[0].LogoUrl!).Should().BeTrue();
    }

    [Fact]
    public async Task Export_WithoutDownloadsAndWithoutPlugins_ClearsChapterPagesAndComicSources_PreservesReadingProgress()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sourceAppData = Path.Combine(_tempRoot, "SourceNoExtras");
        var targetAppData = Path.Combine(_tempRoot, "TargetNoExtras");
        var zipPath = Path.Combine(_tempRoot, "backup_minimal.zip");

        await SeedSampleAppDataAsync(sourceAppData, ct);

        // Act
        await LibraryBackupHelper.ExportArchiveAsync(
            sourceAppData,
            zipPath,
            includeDownloads: false,
            includePlugins: false,
            ct: ct
        );

        using (var zip = ZipFile.OpenRead(zipPath))
        {
            var entryNames = zip.Entries.Select(e => e.FullName).ToList();
            entryNames.Should().Contain("Data/MangaDex/c-fav/cover.jpg");
            entryNames.Should().NotContain(e => e.Contains("ch-1"));
            entryNames.Should().NotContain(e => e.StartsWith("Plugins/"));
        }

        await LibraryBackupHelper.ImportArchiveAsync(
            targetAppData,
            zipPath,
            LibraryImportMode.Replace,
            ct: ct
        );

        // Assert
        var targetDbPath = Path.Combine(targetAppData, "Data", "yukari.db");
        using var conn = new SqliteConnection($"Data Source={targetDbPath};Pooling=False");
        await conn.OpenAsync(ct);

        var pageCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM ChapterPages;");
        pageCount.Should().Be(0);

        var sourceCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM ComicSources;");
        sourceCount.Should().Be(0);

        var chapterUserData = await conn.QuerySingleAsync<(
            int? LastPageRead,
            bool IsDownloaded,
            bool IsRead
        )>("SELECT LastPageRead, IsDownloaded, IsRead FROM ChapterUserData WHERE Id = 'ch-1';");

        chapterUserData.IsDownloaded.Should().BeFalse();
        chapterUserData.IsRead.Should().BeTrue();
        chapterUserData.LastPageRead.Should().Be(12);
    }

    [Fact]
    public async Task Import_MergeMode_MergesCollectionsByNameAndPreservesExistingData()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sourceAppData = Path.Combine(_tempRoot, "MergeSource");
        var targetAppData = Path.Combine(_tempRoot, "MergeTarget");
        var zipPath = Path.Combine(_tempRoot, "backup_merge.zip");

        await SeedSampleAppDataAsync(sourceAppData, ct);
        await LibraryBackupHelper.ExportArchiveAsync(
            sourceAppData,
            zipPath,
            includeDownloads: true,
            includePlugins: false,
            ct: ct
        );

        await LibraryBackupHelper.ExportArchiveAsync(
            targetAppData,
            Path.Combine(_tempRoot, "dummy.zip"),
            false,
            false,
            ct: ct
        );
        var targetDbPath = Path.Combine(targetAppData, "Data", "yukari.db");
        using (var setupConn = new SqliteConnection($"Data Source={targetDbPath};Pooling=False"))
        {
            await setupConn.OpenAsync(ct);
            await setupConn.ExecuteAsync(
                """
                INSERT INTO Collections (Id, Name) VALUES (1, 'SciFi'), (2, 'FavoritesList');
                INSERT INTO Comics (Id, Source, Title, Status, IsAvailable)
                VALUES ('existing-1', 'OtherSource', 'Existing Comic', 'ongoing', 1);
                INSERT INTO ComicUserData (ComicId, Source, IsFavorite)
                VALUES ('existing-1', 'OtherSource', 1);
                INSERT INTO ComicCollections (ComicId, Source, CollectionId)
                VALUES ('existing-1', 'OtherSource', 1);
                INSERT INTO ComicSources (Name, Version, DllPath, IsEnabled)
                VALUES ('ExistingPlugin', '1.0.0', 'C:\Plugins\Existing.dll', 1);
                """
            );
        }

        // Act
        await LibraryBackupHelper.ImportArchiveAsync(
            targetAppData,
            zipPath,
            LibraryImportMode.Merge,
            ct: ct
        );

        // Assert
        using var conn = new SqliteConnection($"Data Source={targetDbPath};Pooling=False");
        await conn.OpenAsync(ct);

        var comicIds = (await conn.QueryAsync<string>("SELECT Id FROM Comics;")).ToList();
        comicIds.Should().BeEquivalentTo(["existing-1", "c-fav"]);

        var mappedCollections = (
            await conn.QueryAsync<string>(
                """
                SELECT col.Name
                FROM ComicCollections cc
                INNER JOIN Collections col ON cc.CollectionId = col.Id
                WHERE cc.ComicId = 'c-fav' AND cc.Source = 'MangaDex';
                """
            )
        ).ToList();
        mappedCollections.Should().ContainSingle().Which.Should().Be("FavoritesList");

        var sources = (await conn.QueryAsync<string>("SELECT Name FROM ComicSources;")).ToList();
        sources.Should().ContainSingle().Which.Should().Be("ExistingPlugin");
    }

    [Fact]
    public async Task Import_MergeMode_DoesNotDowngradeNewerPlugin_AndKeepsMoreRecentLastChapterId()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sourceAppData = Path.Combine(_tempRoot, "MergeVersionSource");
        var targetAppData = Path.Combine(_tempRoot, "MergeVersionTarget");
        var zipPath = Path.Combine(_tempRoot, "backup_merge_version.zip");

        await SeedSampleAppDataAsync(sourceAppData, ct);
        await LibraryBackupHelper.ExportArchiveAsync(
            sourceAppData,
            zipPath,
            includeDownloads: true,
            includePlugins: true,
            ct: ct
        );

        await LibraryBackupHelper.ExportArchiveAsync(
            targetAppData,
            Path.Combine(_tempRoot, "init_merge_version.zip"),
            false,
            false,
            ct: ct
        );

        var targetPluginsDir = Path.Combine(targetAppData, "Plugins");
        Directory.CreateDirectory(targetPluginsDir);
        var targetDllPath = Path.Combine(targetPluginsDir, "MangaDex.dll");
        await File.WriteAllTextAsync(targetDllPath, "newer-v2-dll-bytes", ct);

        var targetDbPath = Path.Combine(targetAppData, "Data", "yukari.db");
        using (var setupConn = new SqliteConnection($"Data Source={targetDbPath};Pooling=False"))
        {
            await setupConn.OpenAsync(ct);
            await setupConn.ExecuteAsync(
                """
                INSERT INTO Comics (Id, Source, Title, Status, IsAvailable)
                VALUES ('c-fav', 'MangaDex', 'Favorite Manga', 'ongoing', 1);
                INSERT INTO ComicUserData (ComicId, Source, IsFavorite, LastReadAt)
                VALUES ('c-fav', 'MangaDex', 1, '2026-10-01T00:00:00Z');
                INSERT INTO ComicReadingProgress (ComicId, Source, Language, LastChapterId)
                VALUES ('c-fav', 'MangaDex', 'en', 'ch-2');
                INSERT INTO ComicSources (Name, Version, DllPath, IsEnabled, PendingRemoval)
                VALUES ('MangaDex', '2.0.0', @DllPath, 1, 0);
                """,
                new { DllPath = targetDllPath }
            );
        }

        // Act
        await LibraryBackupHelper.ImportArchiveAsync(
            targetAppData,
            zipPath,
            LibraryImportMode.Merge,
            ct: ct
        );

        // Assert
        using var conn = new SqliteConnection($"Data Source={targetDbPath};Pooling=False");
        await conn.OpenAsync(ct);

        var version = await conn.ExecuteScalarAsync<string>(
            "SELECT Version FROM ComicSources WHERE Name = 'MangaDex';"
        );
        version.Should().Be("2.0.0");

        var dllBytes = await File.ReadAllTextAsync(targetDllPath, ct);
        dllBytes.Should().Be("newer-v2-dll-bytes");

        var lastChapterId = await conn.ExecuteScalarAsync<string>(
            "SELECT LastChapterId FROM ComicReadingProgress WHERE ComicId = 'c-fav' AND Source = 'MangaDex' AND Language = 'en';"
        );
        lastChapterId.Should().Be("ch-2");
    }

    [Fact]
    public async Task Import_ReplaceMode_PreservesTargetLocalComics_AndPreservesPluginsWhenNotInBackup()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sourceAppData = Path.Combine(_tempRoot, "ReplaceSource");
        var targetAppData = Path.Combine(_tempRoot, "ReplaceTarget");
        var zipPath = Path.Combine(_tempRoot, "backup_replace.zip");

        await SeedSampleAppDataAsync(sourceAppData, ct);
        await LibraryBackupHelper.ExportArchiveAsync(
            sourceAppData,
            zipPath,
            includeDownloads: false,
            includePlugins: false,
            ct: ct
        );

        await LibraryBackupHelper.ExportArchiveAsync(
            targetAppData,
            Path.Combine(_tempRoot, "init.zip"),
            false,
            false,
            ct: ct
        );
        var targetDbPath = Path.Combine(targetAppData, "Data", "yukari.db");
        using (var setupConn = new SqliteConnection($"Data Source={targetDbPath};Pooling=False"))
        {
            await setupConn.OpenAsync(ct);
            await setupConn.ExecuteAsync(
                """
                INSERT INTO Comics (Id, Source, Title, Status, IsAvailable)
                VALUES ('local-target', 'Local', 'My Local Manga', 'ongoing', 1),
                       ('old-online', 'MangaDex', 'Old Online Manga', 'ongoing', 1);
                INSERT INTO ComicUserData (ComicId, Source, IsFavorite)
                VALUES ('local-target', 'Local', 1),
                       ('old-online', 'MangaDex', 1);
                INSERT INTO ComicSources (Name, Version, DllPath, IsEnabled)
                VALUES ('TargetPlugin', '1.0.0', 'C:\Plugins\TargetPlugin.dll', 1);
                """
            );
        }

        // Act
        await LibraryBackupHelper.ImportArchiveAsync(
            targetAppData,
            zipPath,
            LibraryImportMode.Replace,
            ct: ct
        );

        // Assert
        using var conn = new SqliteConnection($"Data Source={targetDbPath};Pooling=False");
        await conn.OpenAsync(ct);

        var comics = (
            await conn.QueryAsync<(string Id, string Source)>("SELECT Id, Source FROM Comics;")
        ).ToList();

        comics.Should().Contain(("local-target", "Local"));
        comics.Should().Contain(("c-fav", "MangaDex"));
        comics.Should().NotContain(("old-online", "MangaDex"));

        var plugins = (await conn.QueryAsync<string>("SELECT Name FROM ComicSources;")).ToList();
        plugins.Should().ContainSingle().Which.Should().Be("TargetPlugin");
    }

    [Fact]
    public async Task Import_ReplaceMode_WhenCancelledBeforeCommit_PreservesExistingFilesAndDatabase()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sourceAppData = Path.Combine(_tempRoot, "CancelSource");
        var targetAppData = Path.Combine(_tempRoot, "CancelTarget");
        var zipPath = Path.Combine(_tempRoot, "backup_cancel.zip");

        await SeedSampleAppDataAsync(sourceAppData, ct);
        await LibraryBackupHelper.ExportArchiveAsync(
            sourceAppData,
            zipPath,
            includeDownloads: true,
            includePlugins: true,
            ct: ct
        );

        await LibraryBackupHelper.ExportArchiveAsync(
            targetAppData,
            Path.Combine(_tempRoot, "init_cancel.zip"),
            false,
            false,
            ct: ct
        );

        var existingComicFile = Path.Combine(
            targetAppData,
            "Data",
            "OldSource",
            "old-1",
            "ch-1",
            "1.jpg"
        );
        Directory.CreateDirectory(Path.GetDirectoryName(existingComicFile)!);
        await File.WriteAllTextAsync(existingComicFile, "existing-page-bytes", ct);

        var targetDbPath = Path.Combine(targetAppData, "Data", "yukari.db");
        using (var setupConn = new SqliteConnection($"Data Source={targetDbPath};Pooling=False"))
        {
            await setupConn.OpenAsync(ct);
            await setupConn.ExecuteAsync(
                """
                INSERT INTO Comics (Id, Source, Title, Status, IsAvailable)
                VALUES ('old-1', 'OldSource', 'Old Manga', 'ongoing', 1);
                INSERT INTO ComicUserData (ComicId, Source, IsFavorite)
                VALUES ('old-1', 'OldSource', 1);
                """
            );
        }

        using var cts = new CancellationTokenSource();
        var progress = new SynchronousProgress<(int Current, int Total)>(p =>
        {
            if (p.Current == 1)
                cts.Cancel();
        });

        // Act
        var act = async () =>
            await LibraryBackupHelper.ImportArchiveAsync(
                targetAppData,
                zipPath,
                LibraryImportMode.Replace,
                progress,
                cts.Token
            );

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        File.Exists(existingComicFile).Should().BeTrue();

        using var conn = new SqliteConnection($"Data Source={targetDbPath};Pooling=False");
        await conn.OpenAsync(ct);
        var comics = (
            await conn.QueryAsync<(string Id, string Source)>("SELECT Id, Source FROM Comics;")
        ).ToList();
        comics.Should().ContainSingle().Which.Should().Be(("old-1", "OldSource"));
    }

    [Fact]
    public async Task ImportArchiveAsync_ThrowsInvalidDataException_WhenArchiveMissingManifest()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var invalidZip = Path.Combine(_tempRoot, "invalid.zip");
        using (var archive = ZipFile.Open(invalidZip, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("random.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("not a backup");
        }

        var targetAppData = Path.Combine(_tempRoot, "InvalidTarget");

        // Act
        var act = async () =>
            await LibraryBackupHelper.ImportArchiveAsync(
                targetAppData,
                invalidZip,
                LibraryImportMode.Merge,
                ct: ct
            );

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }

    private static async Task SeedSampleAppDataAsync(string appDataPath, CancellationToken ct)
    {
        var dataDir = Path.Combine(appDataPath, "Data");
        var dbPath = Path.Combine(dataDir, "yukari.db");

        var initZip = Path.Combine(appDataPath, "init_temp.zip");
        await LibraryBackupHelper.ExportArchiveAsync(appDataPath, initZip, false, false, ct: ct);
        File.Delete(initZip);

        var favCoverDir = Path.Combine(dataDir, "MangaDex", "c-fav");
        var favChapterDir = Path.Combine(favCoverDir, "ch-1");
        Directory.CreateDirectory(favChapterDir);

        var coverPath = Path.Combine(favCoverDir, "cover.jpg");
        var pagePath = Path.Combine(favChapterDir, "1.jpg");
        await File.WriteAllTextAsync(coverPath, "cover-bytes", ct);
        await File.WriteAllTextAsync(pagePath, "page-bytes", ct);

        var pluginsDir = Path.Combine(appDataPath, "Plugins");
        var pluginImagesDir = Path.Combine(pluginsDir, "Images");
        Directory.CreateDirectory(pluginImagesDir);

        var dllPath = Path.Combine(pluginsDir, "MangaDex.dll");
        var logoPath = Path.Combine(pluginImagesDir, "MangaDex.png");
        await File.WriteAllTextAsync(dllPath, "dll-bytes", ct);
        await File.WriteAllTextAsync(logoPath, "logo-bytes", ct);

        using var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False");
        await conn.OpenAsync(ct);

        await conn.ExecuteAsync(
            """
            INSERT INTO Collections (Id, Name) VALUES (1, 'FavoritesList');

            INSERT INTO Comics (Id, Source, Title, Status, CoverImageUrl, IsAvailable)
            VALUES
                ('c-fav', 'MangaDex', 'Favorite Manga', 'ongoing', @CoverPath, 1),
                ('c-unfav', 'MangaDex', 'Unfavorite Manga', 'ongoing', NULL, 1),
                ('c-local', 'Local', 'Local Manga', 'ongoing', 'C:\Local\cover.jpg', 1);

            INSERT INTO ComicUserData (ComicId, Source, IsFavorite, LastSelectedLang, LastReadAt)
            VALUES
                ('c-fav', 'MangaDex', 1, 'en', '2026-05-01T00:00:00Z'),
                ('c-unfav', 'MangaDex', 0, 'en', NULL),
                ('c-local', 'Local', 1, 'Local', NULL);

            INSERT INTO ComicCollections (ComicId, Source, CollectionId)
            VALUES ('c-fav', 'MangaDex', 1);

            INSERT INTO ComicReadingProgress (ComicId, Source, Language, LastChapterId)
            VALUES ('c-fav', 'MangaDex', 'en', 'ch-1');

            INSERT INTO Chapters (Id, ComicId, Source, Title, Language, Pages, SortOrder, IsAvailable)
            VALUES
                ('ch-1', 'c-fav', 'MangaDex', 'Chapter 1', 'en', 15, 0, 1),
                ('ch-local', 'c-local', 'Local', 'Local Chapter 1', 'Local', 10, 0, 1);

            INSERT INTO ChapterUserData (Id, ComicId, Source, LastPageRead, IsDownloaded, IsRead)
            VALUES
                ('ch-1', 'c-fav', 'MangaDex', 12, 1, 1),
                ('ch-local', 'c-local', 'Local', 5, 1, 0);

            INSERT INTO ChapterPages (Number, ChapterId, ComicId, Source, ImageUrl)
            VALUES
                (1, 'ch-1', 'c-fav', 'MangaDex', @PagePath),
                (1, 'ch-local', 'c-local', 'Local', 'C:\Local\1.jpg');

            INSERT INTO ComicSources (Name, Version, DllPath, LogoUrl, IsEnabled, PendingRemoval)
            VALUES
                ('MangaDex', '1.0.0', @DllPath, @LogoPath, 1, 0),
                ('RemovedSource', '1.0.0', 'C:\Old.dll', NULL, 0, 1);
            """,
            new
            {
                CoverPath = coverPath,
                PagePath = pagePath,
                DllPath = dllPath,
                LogoPath = logoPath,
            }
        );
    }
}
