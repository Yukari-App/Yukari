using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Yukari.Services.Storage.Migrations
{
    internal class Migration_001 : IMigration
    {
        public int Version => 1;
        public string Description => "Initial Schema";

        public async Task UpAsync(SqliteConnection connection, DbTransaction transaction)
        {
            await connection.ExecuteAsync(
                """
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

                CREATE TABLE IF NOT EXISTS ComicReadingProgress (
                    ComicId TEXT NOT NULL,
                    Source TEXT NOT NULL,
                    Language TEXT NOT NULL,
                    LastChapterId TEXT,
                    PRIMARY KEY (ComicId, Source, Language)
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
                """,
                transaction: transaction
            );
        }
    }
}
