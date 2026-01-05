using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Yukari.Helpers;
using Yukari.Models;
using Yukari.Models.Data;
using Yukari.Models.DTO;

namespace Yukari.Services.Storage
{
    internal class DataService : IDataService
    {
        private readonly string _connectionString =
            $"Data Source={Path.Combine(AppDataHelper.GetDataPath(), "yukari.db")}";

        public DataService()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA foreign_keys = ON;";
                pragma.ExecuteNonQuery();
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
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
                ";
                command.ExecuteNonQuery();

                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS ComicUserData (
                        ComicId TEXT NOT NULL,
                        Source TEXT NOT NULL,
                        IsFavorite INTEGER NOT NULL DEFAULT 0,
                        LastSelectedLang TEXT,
                        DownloadedLangs TEXT NOT NULL,
                        PRIMARY KEY (ComicId, Source)
                    );
                ";
                command.ExecuteNonQuery();

                command.CommandText = @"
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
                ";
                command.ExecuteNonQuery();

                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS ChapterUserData (
                        Id TEXT NOT NULL,
                        ComicId TEXT NOT NULL,
                        Source TEXT NOT NULL,
                        LastPageRead INTEGER,
                        IsDownloaded INTEGER,
                        IsRead INTEGER,
                        PRIMARY KEY (Id, ComicId, Source)
                    );
                ";
                command.ExecuteNonQuery();

                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS ChapterPages (
                        Id TEXT NOT NULL,
                        ChapterId TEXT NOT NULL,
                        Source TEXT NOT NULL,   
                        PageNumber INTEGER NOT NULL,
                        ImageUrl TEXT NOT NULL,
                        PRIMARY KEY (Id, Source),
                        FOREIGN KEY (ChapterId, Source) REFERENCES Chapters(Id, Source) ON DELETE CASCADE
                    );
                ";
                command.ExecuteNonQuery();

                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS ComicSources (
                        Name TEXT PRIMARY KEY,
                        Version TEXT NOT NULL,
                        LogoUrl TEXT,
                        Description TEXT,
                        DllPath TEXT NOT NULL,
                        IsEnabled INTEGER NOT NULL DEFAULT 1
                    );
                ";
                command.ExecuteNonQuery();
            }

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

        public async Task<IReadOnlyList<ComicModel>> GetFavoriteComicsAsync(string? queryText = null)
        {
            var comics = new List<ComicModel>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT c.Id, c.Source, c.Title, c.CoverImageUrl
                FROM Comics c
                INNER JOIN ComicUserData u 
                    ON c.Id = u.ComicId AND c.Source = u.Source
                WHERE u.IsFavorite = 1
                    AND (
                        $queryText IS NULL OR TRIM($queryText) = ''
                        OR c.Title  LIKE '%' || $queryText || '%' COLLATE NOCASE
                        OR c.Author LIKE '%' || $queryText || '%' COLLATE NOCASE
                    )
            ";

            command.Parameters.AddWithValue("$queryText", queryText?.Trim() ?? string.Empty);

            using var reader = await command.ExecuteReaderAsync();
            while (reader.Read())
            {
                comics.Add(new()
                {
                    Id = reader.GetString(0),
                    Source = reader.GetString(1),
                    Title = reader.GetString(2),
                    CoverImageUrl = reader.IsDBNull(3) ? null : reader.GetString(3),
                });
            }

            return comics;
        }

        public async Task<ComicModel?> GetComicDetailsAsync(ContentKey ComicKey)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT c.Id, c.Source, c.ComicUrl, c.Title, c.Author, c.Description, c.Tags, c.Year, c.CoverImageUrl, c.Langs,
                    u.IsFavorite, u.LastSelectedLang, u.DownloadedLangs
                FROM Comics c
                INNER JOIN ComicUserData u 
                    ON c.Id = u.ComicId AND c.Source = u.Source
                WHERE c.Id = $id AND c.Source = $source;
            ";
            command.Parameters.AddWithValue("$id", ComicKey.Id);
            command.Parameters.AddWithValue("$source", ComicKey.Source);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new ComicModel
                {
                    Id = reader.GetString(0),
                    Source = reader.GetString(1),
                    ComicUrl = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Title = reader.GetString(3),
                    Author = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Description = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Tags = JsonSerializer.Deserialize<string[]>(reader.GetString(6)) ?? Array.Empty<string>(),
                    Year = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    CoverImageUrl = reader.IsDBNull(8) ? null : reader.GetString(8),
                    Langs = JsonSerializer.Deserialize<LanguageModel[]>(reader.GetString(9)) ?? Array.Empty<LanguageModel>()
                };
            }

            return null;
        }

        public async Task<ComicUserData> GetComicUserDataAsync(ContentKey ComicKey)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT IsFavorite, LastSelectedLang, DownloadedLangs
                FROM ComicUserData
                WHERE ComicId = $id AND Source = $source;
            ";
            command.Parameters.AddWithValue("$id", ComicKey.Id);
            command.Parameters.AddWithValue("$source", ComicKey.Source);

            using var reader = command.ExecuteReader();
            if (reader.Read())
                return new ComicUserData
                {
                    IsFavorite = reader.GetInt32(0) == 1,
                    LastSelectedLang = reader.IsDBNull(1) ? null : reader.GetString(1),
                    DownloadedLangs = JsonSerializer.Deserialize<List<string>>(reader.GetString(2)) ?? new List<string>()
                };
            else
                return new();
        }

        public async Task<IReadOnlyList<ChapterModel>> GetAllChaptersAsync(ContentKey comicKey, string language)
        {
            var chapters = new List<ChapterModel>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, ComicId, Source,
                    Title, Number, Volume,
                    Language, Groups, LastUpdate, Pages
                FROM Chapters
                WHERE ComicId = $comicId AND Source = $source AND Language = $language;
            ";
            command.Parameters.AddWithValue("$comicId", comicKey.Id);
            command.Parameters.AddWithValue("$source", comicKey.Source);
            command.Parameters.AddWithValue("$language", language);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                chapters.Add(new()
                {
                    Id = reader.GetString(0),
                    ComicId = reader.GetString(1),
                    Source = reader.GetString(2),
                    Title = reader.GetString(3),
                    Number = reader.GetString(4),
                    Volume = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Language = reader.GetString(6),
                    Groups = reader.IsDBNull(7) ? null : reader.GetString(7),
                    LastUpdate = DateOnly.Parse(reader.GetString(8)),
                    Pages = reader.GetInt32(9)
                });
            }

            return chapters;
        }

        public async Task<ChapterUserData> GetChapterUserDataAsync(ContentKey comicKey, ContentKey chapterKey)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT LastPageRead, IsDownloaded, IsRead
                FROM ChapterUserData
                WHERE Id = $id AND ComicId = $comicId AND Source = $source;
            ";
            command.Parameters.AddWithValue("$id", chapterKey.Id);
            command.Parameters.AddWithValue("$comicId", comicKey.Id);
            command.Parameters.AddWithValue("$source", comicKey.Source);

            using var reader = command.ExecuteReader();
            if (reader.Read())
                return new ChapterUserData
                {
                    LastPageRead = reader.IsDBNull(0) ? null : reader.GetInt32(0),
                    IsDownloaded = reader.IsDBNull(1) ? null : reader.GetInt32(1) == 1,
                    IsRead = reader.IsDBNull(2) ? null : reader.GetInt32(2) == 1
                };
            else
                return new();
        }

        public Task<IReadOnlyList<ChapterPageModel>> GetChapterPagesAsync(ContentKey chapterKey)
        {
            throw new NotImplementedException();
        }

        public async Task<IReadOnlyList<ComicSourceModel>> GetComicSourcesAsync()
        {
            var sources = new List<ComicSourceModel>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT Name, Version, LogoUrl, Description, DllPath, IsEnabled FROM ComicSources;";

            using var reader = await command.ExecuteReaderAsync();
            while (reader.Read())
            {
                sources.Add(new ComicSourceModel
                {
                    Name = reader.GetString(0),
                    Version = reader.GetString(1),
                    LogoUrl = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                    DllPath = reader.GetString(4),
                    IsEnabled = !reader.IsDBNull(5) && reader.GetInt32(5) == 1
                });
            }

            return sources;
        }

        public async Task<ComicSourceModel?> GetComicSourceDetailsAsync(string sourceName)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Name, Version, LogoUrl, Description, DllPath, IsEnabled
                FROM ComicSources 
                WHERE Name = $name;
            ";
            command.Parameters.AddWithValue("$name", sourceName);

            using var reader = await command.ExecuteReaderAsync();
            if (reader.Read())
            {
                return new ComicSourceModel
                {
                    Name = reader.GetString(0),
                    Version = reader.GetString(1),
                    LogoUrl = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                    DllPath = reader.GetString(4),
                    IsEnabled = !reader.IsDBNull(5) && reader.GetInt32(5) == 1
                };
            }

            return null;
        }

        public async Task<bool> InsertFavoriteComicAsync(ComicModel comic)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"
                        INSERT OR IGNORE INTO Comics 
                        (Id, Source, ComicUrl, Title, Author, Description, Tags, Year, CoverImageUrl, Langs)
                        VALUES ($id, $source, $comicUrl, $title, $author, $description, $tags, $year, $coverImageUrl, $langs);
                    ";

                    command.Parameters.AddWithValue("$id", comic.Id);
                    command.Parameters.AddWithValue("$source", comic.Source);
                    command.Parameters.AddWithValue("$comicUrl", comic.ComicUrl ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$title", comic.Title);
                    command.Parameters.AddWithValue("$author", comic.Author ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$description", comic.Description ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$tags", JsonSerializer.Serialize(comic.Tags));
                    command.Parameters.AddWithValue("$year", comic.Year ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$coverImageUrl", comic.CoverImageUrl ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$langs", JsonSerializer.Serialize(comic.Langs));
                    await command.ExecuteNonQueryAsync();
                }

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"
                        INSERT INTO ComicUserData (ComicId, Source, IsFavorite, DownloadedLangs)
                        VALUES ($comicId, $source, $isFavorite, $DownloadedLangs)
                        ON CONFLICT(ComicId, Source) DO UPDATE SET
                            IsFavorite = excluded.IsFavorite,
                            DownloadedLangs = excluded.DownloadedLangs;
                    ";

                    command.Parameters.AddWithValue("$comicId", comic.Id);
                    command.Parameters.AddWithValue("$source", comic.Source);
                    command.Parameters.AddWithValue("$isFavorite", 1);
                    command.Parameters.AddWithValue("$DownloadedLangs", JsonSerializer.Serialize(new List<string>()));
                    await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
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
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        INSERT INTO ComicUserData (ComicId, Source, IsFavorite, LastSelectedLang, DownloadedLangs)
                        VALUES ($comicId, $source, $isFavorite, $lastSelectedLang, $downloadedLangs)
                        ON CONFLICT(ComicId, Source) DO UPDATE SET
                            IsFavorite = excluded.IsFavorite,
                            LastSelectedLang = excluded.LastSelectedLang,
                            DownloadedLangs = excluded.DownloadedLangs;
                    ";

                    command.Parameters.AddWithValue("$comicId", comicKey.Id);
                    command.Parameters.AddWithValue("$source", comicKey.Source);
                    command.Parameters.AddWithValue("$isFavorite", comicUserData.IsFavorite);
                    command.Parameters.AddWithValue("$lastSelectedLang", comicUserData.LastSelectedLang ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$downloadedLangs", JsonSerializer.Serialize(comicUserData.DownloadedLangs));
                    await command.ExecuteNonQueryAsync();
                    return true;
                }
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
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        INSERT INTO Chapters 
                        (Id, ComicId, Source, Title, Number, Volume, Language, Groups, LastUpdate, Pages)
                        VALUES ($id, $comicId, $source, $title, $number, $volume, $language, $groups, $lastUpdate, $pages)
                        ON CONFLICT(Id, ComicId, Source) DO UPDATE SET
                            Title = excluded.Title,
                            Number = excluded.Number,
                            Volume = excluded.Volume,
                            Language = excluded.Language,
                            Groups = excluded.Groups,
                            LastUpdate = excluded.LastUpdate,
                            Pages = excluded.Pages;
                    ";

                    command.Parameters.AddWithValue("$id", chapter.Id);
                    command.Parameters.AddWithValue("$comicId", chapter.ComicId);
                    command.Parameters.AddWithValue("$source", chapter.Source);
                    command.Parameters.AddWithValue("$title", chapter.Title ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$number", chapter.Number);
                    command.Parameters.AddWithValue("$volume", chapter.Volume ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$language", chapter.Language);
                    command.Parameters.AddWithValue("$groups", chapter.Groups ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$lastUpdate", chapter.LastUpdate.ToString("yyyy-MM-dd"));
                    command.Parameters.AddWithValue("$pages", chapter.Pages);
                    await command.ExecuteNonQueryAsync();
                    return true;
                }
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
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        INSERT INTO ChapterUserData (Id, ComicId, Source, LastPageRead, IsDownloaded, IsRead)
                        VALUES ($id, $comicId, $source, $lastPageRead, $isDownloaded, $isRead)
                        ON CONFLICT(Id, ComicId, Source) DO UPDATE SET
                            LastPageRead = excluded.LastPageRead,
                            IsDownloaded = excluded.IsDownloaded,
                            IsRead = excluded.IsRead;
                    ";

                    command.Parameters.AddWithValue("$id", chapterKey.Id);
                    command.Parameters.AddWithValue("$comicId", comicKey.Id);
                    command.Parameters.AddWithValue("$source", comicKey.Source);
                    command.Parameters.AddWithValue("$lastPageRead", chapterUserData.LastPageRead ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$isDownloaded", chapterUserData.IsDownloaded);
                    command.Parameters.AddWithValue("$isRead", chapterUserData.IsRead);
                    await command.ExecuteNonQueryAsync();
                    return true;
                }
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
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        INSERT OR IGNORE INTO ComicSources
                        (Name, Version, LogoUrl, Description, DllPath, IsEnabled)
                        VALUES ($name, $version, $logoUrl, $description, $dllPath, $isEnabled);
                    ";

                    command.Parameters.AddWithValue("$name", comicSource.Name);
                    command.Parameters.AddWithValue("$version", comicSource.Version);
                    command.Parameters.AddWithValue("$logoUrl", comicSource.LogoUrl ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$description", comicSource.Description ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("$dllPath", comicSource.DllPath);
                    command.Parameters.AddWithValue("$isEnabled", comicSource.IsEnabled ? 1 : 0);

                    await command.ExecuteNonQueryAsync();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public Task<bool> RemoveFavoriteComicAsync(ContentKey ComicKey)
        {
            throw new NotImplementedException();
        }

        public Task<bool> RemoveChapterAsync(ContentKey chapterKey)
        {
            throw new NotImplementedException();
        }

        public Task<bool> RemoveComicSourceAsync(string sourceName)
        {
            throw new NotImplementedException();
        }

        public Task<bool> CleanupUnfavoriteComicsDataAsync()
        {
            throw new NotImplementedException();
        }
    }
}
