using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Yukari.Helpers;
using Yukari.Helpers.Database;
using Yukari.Models;
using Yukari.Models.Data;
using Yukari.Models.DTO;

namespace Yukari.Services.Storage
{
    internal class DataService : IDataService
    {
        private readonly string _connectionString =
            $"Data Source={Path.Combine(AppDataHelper.GetDataPath(), "yukari.db")}";

        static DataService()
        {
            SqlMapper.AddTypeHandler(new JsonStringArrayHandler());
            SqlMapper.AddTypeHandler(new JsonStringListHandler());
            SqlMapper.AddTypeHandler(new LanguageArrayHandler());
            SqlMapper.AddTypeHandler(new DateOnlyHandler());
        }

        public DataService()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            connection.Execute("PRAGMA foreign_keys = ON;");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Comics (
                    Id TEXT NOT NULL,
                    Source TEXT NOT NULL,
                    ComicUrl TEXT,
                    Title TEXT NOT NULL,
                    Author TEXT,
                    Description TEXT,
                    Tags TEXT,
                    Year INTEGER,
                    CoverImageUrl TEXT,
                    Langs TEXT,
                    IsAvailable INTEGER NOT NULL DEFAULT 1,
                    PRIMARY KEY (Id, Source)
                );

                CREATE TABLE IF NOT EXISTS ComicUserData (
                    ComicId TEXT NOT NULL,
                    Source TEXT NOT NULL,
                    IsFavorite INTEGER NOT NULL DEFAULT 0,
                    LastSelectedLang TEXT,
                    DownloadedLangs TEXT NOT NULL,
                    PRIMARY KEY (ComicId, Source)
                );

                CREATE TABLE IF NOT EXISTS Chapters (
                    Id TEXT NOT NULL,
                    ComicId TEXT NOT NULL,
                    Source TEXT NOT NULL,
                    Title TEXT,
                    Number TEXT,
                    Volume TEXT,
                    Language TEXT NOT NULL,
                    Groups TEXT,
                    LastUpdate TEXT,
                    Pages INTEGER NOT NULL,
                    IsAvailable INTEGER NOT NULL DEFAULT 1,
                    PRIMARY KEY (Id, ComicId, Source)
                );

                CREATE TABLE IF NOT EXISTS ChapterUserData (
                    Id TEXT NOT NULL,
                    ComicId TEXT NOT NULL,
                    Source TEXT NOT NULL,
                    LastPageRead INTEGER,
                    IsDownloaded INTEGER,
                    IsRead INTEGER,
                    PRIMARY KEY (Id, ComicId, Source)
                );

                CREATE TABLE IF NOT EXISTS ChapterPages (
                    Id TEXT NOT NULL,
                    ChapterId TEXT NOT NULL,
                    Source TEXT NOT NULL,   
                    PageNumber INTEGER NOT NULL,
                    ImageUrl TEXT NOT NULL,
                    PRIMARY KEY (Id, Source),
                    FOREIGN KEY (ChapterId, Source) REFERENCES Chapters(Id, Source) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ComicSources (
                    Name TEXT PRIMARY KEY,
                    Version TEXT NOT NULL,
                    LogoUrl TEXT,
                    Description TEXT,
                    DllPath TEXT NOT NULL,
                    IsEnabled INTEGER NOT NULL DEFAULT 1
                );
            ");
        }

        private async Task<SqliteConnection> GetOpenConnectionAsync()
        {
            var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            await connection.ExecuteAsync("PRAGMA foreign_keys = ON;");
            return connection;
        }

        public async Task<IReadOnlyList<ComicModel>> GetFavoriteComicsAsync(string? queryText = null)
        {
            using var connection = await GetOpenConnectionAsync();

            const string sql = @"
                SELECT c.Id, c.Source, c.Title, c.CoverImageUrl
                FROM Comics c
                INNER JOIN ComicUserData u 
                    ON c.Id = u.ComicId AND c.Source = u.Source
                WHERE u.IsFavorite = 1
                    AND (
                        @QueryText IS NULL OR TRIM(@QueryText) = ''
                        OR c.Title  LIKE '%' || @QueryText || '%' COLLATE NOCASE
                        OR c.Author LIKE '%' || @QueryText || '%' COLLATE NOCASE
                    )
            ";

            var result = await connection.QueryAsync<ComicModel>(sql, new
            {
                QueryText = queryText?.Trim()
            });

            return result.ToList();
        }

        public async Task<ComicModel?> GetComicDetailsAsync(ContentKey ComicKey)
        {
            using var connection = await GetOpenConnectionAsync();

            const string sql = @"
                SELECT 
                    c.Id, c.Source, c.ComicUrl, c.Title, c.Author, c.Description, 
                    c.Tags, c.Year, c.CoverImageUrl, c.Langs,
                    u.IsFavorite, u.LastSelectedLang, u.DownloadedLangs
                FROM Comics c
                INNER JOIN ComicUserData u 
                    ON c.Id = u.ComicId AND c.Source = u.Source
                WHERE c.Id = @Id AND c.Source = @Source;
            ";

            return await connection.QueryFirstOrDefaultAsync<ComicModel>(sql, new
            {
                Id = ComicKey.Id,
                Source = ComicKey.Source
            });
        }

        public async Task<ComicUserData> GetComicUserDataAsync(ContentKey ComicKey)
        {
            using var connection = await GetOpenConnectionAsync();

            const string sql = @"
                SELECT IsFavorite, LastSelectedLang, DownloadedLangs
                FROM ComicUserData
                WHERE ComicId = @Id AND Source = @Source;
            ";

            var result = await connection.QueryFirstOrDefaultAsync<ComicUserData>(sql, new
            {
                Id = ComicKey.Id,
                Source = ComicKey.Source
            });

            return result ?? new ComicUserData();
        }

        public async Task<IReadOnlyList<ChapterModel>> GetAllChaptersAsync(ContentKey comicKey, string language)
        {
            using var connection = await GetOpenConnectionAsync();

            const string sql = @"
                SELECT Id, ComicId, Source,
                       Title, Number, Volume,
                       Language, Groups, LastUpdate, Pages
                FROM Chapters
                WHERE ComicId = @Id AND Source = @Source AND Language = @Language;
            ";

            var result = await connection.QueryAsync<ChapterModel>(sql, new
            {
                Id = comicKey.Id,
                Source = comicKey.Source,
                Language = language
            });

            return result.ToList();
        }

        public async Task<Dictionary<string, ChapterUserData>> GetAllChaptersUserDataMapAsync(ContentKey comicKey)
        {
            using var connection = await GetOpenConnectionAsync();

            const string sql = @"
                SELECT Id, LastPageRead, IsDownloaded, IsRead 
                FROM ChapterUserData
                WHERE ComicId = @Id AND Source = @Source
            ";

            var result = await connection.QueryAsync<(string Id, int? LastPageRead, bool IsDownloaded, bool IsRead)>(
                sql,
                new { Id = comicKey.Id, Source = comicKey.Source }
            );

            return result.ToDictionary(
            x => x.Id,
            x => new ChapterUserData
            {
                LastPageRead = x.LastPageRead,
                IsDownloaded = x.IsDownloaded,
                IsRead = x.IsRead
            });
        }

        public async Task<ChapterUserData> GetChapterUserDataAsync(ContentKey comicKey, ContentKey chapterKey)
        {
            using var connection = await GetOpenConnectionAsync();

            const string sql = @"
                SELECT LastPageRead, IsDownloaded, IsRead
                FROM ChapterUserData
                WHERE Id = @id AND ComicId = @comicId AND Source = @source;
            ";

            var result = await connection.QueryFirstOrDefaultAsync<ChapterUserData>(sql, new
            {
                id = chapterKey.Id,
                comicId = comicKey.Id,
                source = comicKey.Source
            });

            return result ?? new ChapterUserData();
        }

        public Task<IReadOnlyList<ChapterPageModel>> GetChapterPagesAsync(ContentKey comicKey, ContentKey chapterKey)
        {
            throw new NotImplementedException();
        }

        public async Task<IReadOnlyList<ComicSourceModel>> GetComicSourcesAsync()
        {
            using var connection = await GetOpenConnectionAsync();

            const string sql = @"SELECT Name, Version, LogoUrl, Description, DllPath, IsEnabled FROM ComicSources;";

            var result = await connection.QueryAsync<ComicSourceModel>(sql);
            return result.ToList();
        }

        public async Task<ComicSourceModel?> GetComicSourceDetailsAsync(string sourceName)
        {
            using var connection = await GetOpenConnectionAsync();

            const string sql = @"
                SELECT Name, Version, LogoUrl, Description, DllPath, IsEnabled
                FROM ComicSources 
                WHERE Name = @name;
            ";

            return await connection.QueryFirstOrDefaultAsync<ComicSourceModel>(sql, new
            {
                name = sourceName
            });
        }

        public async Task UpsertFavoriteComicAsync(ComicModel comic)
        {
            using var connection = await GetOpenConnectionAsync();

            using var transaction = connection.BeginTransaction();

            const string sqlComic = @"
                INSERT INTO Comics 
                (Id, Source, ComicUrl, Title, Author, Description, Tags, Year, CoverImageUrl, Langs, IsAvailable)
                VALUES (@Id, @Source, @ComicUrl, @Title, @Author, @Description, @Tags, @Year, @CoverImageUrl, @Langs, @IsAvailable)
                ON CONFLICT(Id, Source) DO UPDATE SET
                    ComicUrl = excluded.ComicUrl,
                    Title = excluded.Title,
                    Author = excluded.Author,
                    Description = excluded.Description,
                    Tags = excluded.Tags,
                    Year = excluded.Year,
                    CoverImageUrl = excluded.CoverImageUrl,
                    Langs = excluded.Langs,
                    IsAvailable = excluded.IsAvailable;
            ";

            await connection.ExecuteAsync(sqlComic, comic, transaction);

            const string sqlUserData = @"
            INSERT INTO ComicUserData (ComicId, Source, IsFavorite, DownloadedLangs)
            VALUES (@ComicId, @Source, @IsFavorite, @DownloadedLangs)
                ON CONFLICT(ComicId, Source) DO UPDATE SET
                IsFavorite = excluded.IsFavorite;
            ";

            await connection.ExecuteAsync(sqlUserData, new
            {
                ComicId = comic.Id,
                Source = comic.Source,
                IsFavorite = true,
                DownloadedLangs = "[]"
            }, transaction);

            transaction.Commit();
        }

        public async Task UpsertComicUserDataAsync(ContentKey comicKey, ComicUserData comicUserData)
        {
            using var connection = await GetOpenConnectionAsync();

            const string sql = @"
                INSERT INTO ComicUserData (ComicId, Source, IsFavorite, LastSelectedLang, DownloadedLangs)
                VALUES (@ComicId, @Source, @IsFavorite, @LastSelectedLang, @DownloadedLangs)
                ON CONFLICT(ComicId, Source) DO UPDATE SET
                    IsFavorite = excluded.IsFavorite,
                    LastSelectedLang = COALESCE(excluded.LastSelectedLang, ComicUserData.LastSelectedLang),
                    DownloadedLangs = COALESCE(excluded.DownloadedLangs, ComicUserData.DownloadedLangs);
            ";

            await connection.ExecuteAsync(sql, new
            {
                ComicId = comicKey.Id,
                Source = comicKey.Source,
                comicUserData.IsFavorite,
                comicUserData.LastSelectedLang,
                comicUserData.DownloadedLangs
            });
        }

        public async Task UpsertChapterAsync(ChapterModel chapter)
        {
            using var connection = await GetOpenConnectionAsync();

            const string sql = @"
                INSERT INTO Chapters 
                (Id, ComicId, Source, Title, Number, Volume, Language, Groups, LastUpdate, Pages)
                VALUES (@Id, @ComicId, @Source, @Title, @Number, @Volume, @Language, @Groups, @LastUpdate, @Pages)
                ON CONFLICT(Id, ComicId, Source) DO UPDATE SET
                    Title = excluded.Title,
                    Number = excluded.Number,
                    Volume = excluded.Volume,
                    Language = excluded.Language,
                    Groups = excluded.Groups,
                    LastUpdate = excluded.LastUpdate,
                    Pages = excluded.Pages;
            ";

            await connection.ExecuteAsync(sql, chapter);
        }

        public async Task UpsertChaptersAsync(ContentKey comicKey, string language, IEnumerable<ChapterModel> chapters)
        {
            using var connection = await GetOpenConnectionAsync();
            using var transaction = connection.BeginTransaction();

            const string sqlReset = @"
                UPDATE Chapters 
                SET IsAvailable = 0 
                WHERE ComicId = @ComicId AND Source = @Source AND Language = @Language;
            ";

            await connection.ExecuteAsync(sqlReset, new
            {
                ComicId = comicKey.Id,
                Source = comicKey.Source,
                Language = language 
            }, transaction);

            const string sqlUpsert = @"
                INSERT INTO Chapters 
                (Id, ComicId, Source, Title, Number, Volume, Language, Groups, LastUpdate, Pages, IsAvailable)
                VALUES (@Id, @ComicId, @Source, @Title, @Number, @Volume, @Language, @Groups, @LastUpdate, @Pages, 1)
                ON CONFLICT(Id, ComicId, Source) DO UPDATE SET
                    Title = excluded.Title,
                    Number = excluded.Number,
                    Volume = excluded.Volume,
                    Language = excluded.Language,
                    Groups = excluded.Groups,
                    LastUpdate = excluded.LastUpdate,
                    Pages = excluded.Pages,
                    IsAvailable = 1;
            ";

            await connection.ExecuteAsync(sqlUpsert, chapters, transaction);

            transaction.Commit();
        }

        public async Task UpsertChapterUserDataAsync(ContentKey comicKey, ContentKey chapterKey, ChapterUserData chapterUserData)
        {
            using var connection = await GetOpenConnectionAsync();

            const string sql = @"
                INSERT INTO ChapterUserData (Id, ComicId, Source, LastPageRead, IsDownloaded, IsRead)
                VALUES (@Id, @ComicId, @Source, @LastPageRead, @IsDownloaded, @IsRead)
                ON CONFLICT(Id, ComicId, Source) DO UPDATE SET
                    LastPageRead = COALESCE(excluded.LastPageRead, ChapterUserData.LastPageRead),
                    IsDownloaded = excluded.IsDownloaded,
                    IsRead = excluded.IsRead;
            ";

            await connection.ExecuteAsync(sql, new
            {
                Id = chapterKey.Id,
                ComicId = comicKey.Id,
                Source = comicKey.Source,
                chapterUserData.LastPageRead,
                chapterUserData.IsDownloaded,
                chapterUserData.IsRead
            });
        }

        public Task UpsertChapterPagesAsync(IReadOnlyList<ChapterPageModel> chapterPages)
        {
            throw new NotImplementedException();
        }

        public async Task UpsertComicSourceAsync(ComicSourceModel comicSource)
        {
            using var connection = await GetOpenConnectionAsync();

            const string sql = @"
            INSERT INTO ComicSources (Name, Version, LogoUrl, Description, DllPath, IsEnabled)
            VALUES (@Name, @Version, @LogoUrl, @Description, @DllPath, @IsEnabled)
            ON CONFLICT(Name) DO UPDATE SET
                Version = excluded.Version,
                LogoUrl = excluded.LogoUrl,
                Description = excluded.Description,
                DllPath = excluded.DllPath,
                IsEnabled = excluded.IsEnabled;
            ";

            await connection.ExecuteAsync(sql, comicSource);
        }

        public async Task RemoveFavoriteComicAsync(ContentKey comicKey)
        {
            using var connection = await GetOpenConnectionAsync();

            const string sql = @"
                UPDATE ComicUserData 
                SET IsFavorite = 0,
                    DownloadedLangs = '[]'
                WHERE ComicId = @Id AND Source = @Source;
            ";

            await connection.ExecuteAsync(sql, new
            {
                Id = comicKey.Id,
                Source = comicKey.Source
            });
        }

        public async Task RemoveChapterAsync(ContentKey comicKey, ContentKey chapterKey)
        {
            using var connection = await GetOpenConnectionAsync();
            using var transaction = connection.BeginTransaction();

            await connection.ExecuteAsync(
                "DELETE FROM ChapterUserData WHERE Id = @Id AND ComicId = @ComicId AND Source = @Source;",
                new { Id = chapterKey.Id, ComicId = comicKey.Id, Source = chapterKey.Source },
                transaction);

            await connection.ExecuteAsync(
                    "DELETE FROM Chapters WHERE Id = @Id AND ComicId = @ComicId AND Source = @Source;",
                    new { Id = chapterKey.Id, ComicId = comicKey.Id, Source = chapterKey.Source },
                    transaction);

            transaction.Commit();
        }

        public async Task RemoveComicSourceAsync(string sourceName)
        {
            using var connection = await GetOpenConnectionAsync();

            const string sql = "DELETE FROM ComicSources WHERE Name = @Name;";
            await connection.ExecuteAsync(sql, new { Name = sourceName });
        }

        public async Task CleanupUnfavoriteComicsDataAsync()
        {
            using var connection = await GetOpenConnectionAsync();
            using var transaction = connection.BeginTransaction();

            const string sqlDeleteChapters = @"
                DELETE FROM Chapters 
                WHERE NOT EXISTS (
                    SELECT 1 FROM ComicUserData u 
                    WHERE u.ComicId = Chapters.ComicId 
                        AND u.Source = Chapters.Source 
                        AND u.IsFavorite = 1
                );
            ";

            const string sqlDeleteChapterUserData = @"
                DELETE FROM ChapterUserData 
                WHERE NOT EXISTS (
                    SELECT 1 FROM ComicUserData u 
                    WHERE u.ComicId = ChapterUserData.ComicId 
                        AND u.Source = ChapterUserData.Source 
                        AND u.IsFavorite = 1
                );
            ";

            const string sqlDeleteComics = @"
                DELETE FROM Comics 
                WHERE NOT EXISTS (
                    SELECT 1 FROM ComicUserData u 
                    WHERE u.ComicId = Comics.Id 
                        AND u.Source = Comics.Source 
                        AND u.IsFavorite = 1
                );
            ";

            const string sqlDeleteComicUserData = @"DELETE FROM ComicUserData WHERE IsFavorite = 0;";

            await connection.ExecuteAsync(sqlDeleteChapters, transaction: transaction);
            await connection.ExecuteAsync(sqlDeleteChapterUserData, transaction: transaction);
            await connection.ExecuteAsync(sqlDeleteComics, transaction: transaction);
            await connection.ExecuteAsync(sqlDeleteComicUserData, transaction: transaction);

            transaction.Commit();
            await connection.ExecuteAsync("VACUUM;");
        }
    }
}