using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
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
                    Number TEXT NOT NULL,
                    Volume TEXT,
                    Language TEXT NOT NULL,
                    Groups TEXT,
                    LastUpdate TEXT,
                    Pages INTEGER NOT NULL,
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

            // MOCK
            _ = UpsertComicSourceAsync(new ComicSourceModel()
            {
                Name = "MangaDex",
                Version = "1.2.0+core1.2.0",
                Description = "A community-driven Comic database and reader.",
                LogoUrl = "https://mangadex.org/img/brand/mangadex-logo.svg",
                DllPath = Path.Combine(
                    AppDataHelper.GetPluginsPath(),
                    "Yukari.Plugin.MangaDex.dll"
                ),
                IsEnabled = true
            });
            
            _ = InsertFavoriteComicAsync(new ComicModel
            {
                Id = "f8fed9b2-546f-446f-bd3f-3c7192019774",
                Source = "MangaDex",
                Title = "Nazo no Kanojo X",
                Author = "Ueshiba Riichi",
                Description = "One day, a strange transfer student appears before Tsubaki. Urabe Mikoto is an antisocial girl, whose hobby is just sleeping during class-breaks. \nOne day, Tsubaki goes to wake her up and accidentally tastes her drool… And gets hooked on that!  \n  \nAfter that, he starts going out with her and gets to know her better. Her second hobby, as it turns out, is carrying around scissors in her panties \nand cutting paper into flowers… or whatever else is getting on her nerves…",
                Tags = ["Comedy", "Ecchi", "Mystery", "Romance", "School Life", "Supernatural"],
                Year = 2006,
                CoverImageUrl = @"https://mangadex.org/covers/f8fed9b2-546f-446f-bd3f-3c7192019774/7ae6a847-c337-42c1-b725-b7f25bae7c54.jpg",
                Langs = [new("pt-br", "Português"), new("en", "English"), new("es", "Espanol")],
            });

            _ = InsertFavoriteComicAsync(new ComicModel
            {
                Id = "2",
                Source = "MangaDex",
                Title = "Mahou Shoujo ni Akogarete",
                Author = "Akihiro Ononaka",
                Description = "Utena, uma estudante comum do ensino médio e que adora garotas mágicas se encontra com uma criatura chamada Venalita e adquire poderes. Mas, diferente do esperado, ela não se torna uma garota mágica, mas uma vilã que, agora, deverá perseguir e derrotar suas ídolas.",
                Tags = ["Action", "Comedy", "Demons", "Ecchi", "Fantasy", "Magic", "Magical Girls", "Sexual Violence", "Superhero", "Yuri"],
                Year = 2019,
                CoverImageUrl = @"https://meo.comick.pictures/8yKOQe.jpg",
                Langs = [new("pt-br", "Português"), new("en", "English")],
            });

            _ = UpsertChapterAsync(new ChapterModel
            {
                Id = "1",
                ComicId = "2",
                Source = "MangaDex",
                Title = "",
                Number = "1",
                Volume = "1",
                Language = "pt-br",
                Groups = "White Wolves",
                LastUpdate = new DateOnly(2019, 02, 06),
                Pages = 29
            });

            _ = UpsertChapterUserDataAsync(new ContentKey("f8fed9b2-546f-446f-bd3f-3c7192019774", "MangaDex"), new ContentKey("23e4f77c-7906-4221-9a2d-28b8dabffc22", "MangaDex"), new ChapterUserData
            {
                LastPageRead = 6,
                IsDownloaded = false,
                IsRead = true
            });
            // MOCK END
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

            var result = await connection.QueryAsync<string, ChapterUserData, (string Id, ChapterUserData Data)>(
                sql,
                (id, data) => (id, data),
                new { Id = comicKey.Id, Source = comicKey.Source },
                splitOn: "LastPageRead"
            );

            return result.ToDictionary(x => x.Id, x => x.Data);
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

        public Task<IReadOnlyList<ChapterPageModel>> GetChapterPagesAsync(ContentKey chapterKey)
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

        public async Task<bool> InsertFavoriteComicAsync(ComicModel comic)
        {
            try
            {
                using var connection = await GetOpenConnectionAsync();

                using var transaction = connection.BeginTransaction();

                const string sqlComic = @"
                    INSERT OR IGNORE INTO Comics 
                    (Id, Source, ComicUrl, Title, Author, Description, Tags, Year, CoverImageUrl, Langs)
                    VALUES (@Id, @Source, @ComicUrl, @Title, @Author, @Description, @Tags, @Year, @CoverImageUrl, @Langs);
                ";

                await connection.ExecuteAsync(sqlComic, comic, transaction);

                const string sqlUserData = @"
                    INSERT INTO ComicUserData (ComicId, Source, IsFavorite, DownloadedLangs)
                    VALUES (@ComicId, @Source, @IsFavorite, @DownloadedLangs)
                    ON CONFLICT(ComicId, Source) DO UPDATE SET
                        IsFavorite = excluded.IsFavorite,
                        DownloadedLangs = excluded.DownloadedLangs;
                ";

                await connection.ExecuteAsync(sqlUserData, new
                {
                    ComicId = comic.Id,
                    Source = comic.Source,
                    IsFavorite = true,
                    DownloadedLangs = new List<string>()
                }, transaction);

                transaction.Commit();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpsertComicUserDataAsync(ContentKey comicKey, ComicUserData comicUserData)
        {
            try
            {
                using var connection = await GetOpenConnectionAsync();

                const string sql = @"
                    INSERT INTO ComicUserData (ComicId, Source, IsFavorite, LastSelectedLang, DownloadedLangs)
                    VALUES (@ComicId, @Source, @IsFavorite, @LastSelectedLang, @DownloadedLangs)
                    ON CONFLICT(ComicId, Source) DO UPDATE SET
                        IsFavorite = excluded.IsFavorite,
                        LastSelectedLang = excluded.LastSelectedLang,
                        DownloadedLangs = excluded.DownloadedLangs;
                ";

                await connection.ExecuteAsync(sql, new
                {
                    ComicId = comicKey.Id,
                    Source = comicKey.Source,
                    comicUserData.IsFavorite,
                    comicUserData.LastSelectedLang,
                    comicUserData.DownloadedLangs
                });

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpsertChapterAsync(ChapterModel chapter)
        {
            try
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
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpsertChapterUserDataAsync(ContentKey comicKey, ContentKey chapterKey, ChapterUserData chapterUserData)
        {
            try
            {
                using var connection = await GetOpenConnectionAsync();

                const string sql = @"
                    INSERT INTO ChapterUserData (Id, ComicId, Source, LastPageRead, IsDownloaded, IsRead)
                    VALUES (@Id, @ComicId, @Source, @LastPageRead, @IsDownloaded, @IsRead)
                    ON CONFLICT(Id, ComicId, Source) DO UPDATE SET
                        LastPageRead = excluded.LastPageRead,
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

                return true;
            }
            catch
            {
                return false;
            }
        }

        public Task<bool> UpsertChapterPagesAsync(IReadOnlyList<ChapterPageModel> chapterPages)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> UpsertComicSourceAsync(ComicSourceModel comicSource)
        {
            try
            {
                using var connection = await GetOpenConnectionAsync();

                const string sql = @"
                    INSERT OR IGNORE INTO ComicSources
                    (Name, Version, LogoUrl, Description, DllPath, IsEnabled)
                    VALUES (@Name, @Version, @LogoUrl, @Description, @DllPath, @IsEnabled);
                ";

                await connection.ExecuteAsync(sql, comicSource);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RemoveFavoriteComicAsync(ContentKey comicKey)
        {
            try
            {
                using var connection = await GetOpenConnectionAsync();

                const string sql = @"
                    UPDATE ComicUserData 
                    SET IsFavorite = 0,
                        DownloadedLangs = '[]'
                    WHERE ComicId = @Id AND Source = @Source;
                ";

                var rowsAffected = await connection.ExecuteAsync(sql, new
                {
                    Id = comicKey.Id,
                    Source = comicKey.Source
                });

                return rowsAffected > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RemoveChapterAsync(ContentKey comicKey, ContentKey chapterKey)
        {
            try
            {
                using var connection = await GetOpenConnectionAsync();
                using var transaction = connection.BeginTransaction();

                await connection.ExecuteAsync(
                    "DELETE FROM ChapterUserData WHERE Id = @Id AND ComicId = @ComicId AND Source = @Source;",
                    new { Id = chapterKey.Id, ComicId = comicKey.Id, Source = chapterKey.Source },
                    transaction);

                var rowsAffected = await connection.ExecuteAsync(
                    "DELETE FROM Chapters WHERE Id = @Id AND ComicId = @ComicId AND Source = @Source;",
                    new { Id = chapterKey.Id, ComicId = comicKey.Id, Source = chapterKey.Source },
                    transaction);

                transaction.Commit();
                return rowsAffected > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RemoveComicSourceAsync(string sourceName)
        {
            try
            {
                using var connection = await GetOpenConnectionAsync();

                const string sql = "DELETE FROM ComicSources WHERE Name = @Name;";

                var rowsAffected = await connection.ExecuteAsync(sql, new { Name = sourceName });
                return rowsAffected > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> CleanupUnfavoriteComicsDataAsync()
        {
            try
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

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}