using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Yukari.Services.Storage.Migrations;

internal class Migration_002 : IMigration
{
    public int Version => 2;
    public string Description => "Add Collections system and fix schema inconsistencies";

    public async Task UpAsync(SqliteConnection connection, DbTransaction transaction)
    {
        // 1. Create Collections and ComicCollections tables
        await connection.ExecuteAsync(
            """
            CREATE TABLE IF NOT EXISTS Collections (
                Id INTEGER PRIMARY KEY,
                Name TEXT NOT NULL UNIQUE
            );

            CREATE TABLE IF NOT EXISTS ComicCollections (
                ComicId TEXT NOT NULL,
                Source TEXT NOT NULL,
                CollectionId INTEGER NOT NULL,
                PRIMARY KEY (ComicId, Source, CollectionId),
                FOREIGN KEY (ComicId, Source) REFERENCES Comics(Id, Source) ON DELETE CASCADE,
                FOREIGN KEY (CollectionId) REFERENCES Collections(Id) ON DELETE CASCADE
            );
            """,
            transaction: transaction
        );

        // Migration_001 had a redundant UNIQUE (Id, Source) constraint on Chapters alongside
        // PRIMARY KEY (Id, ComicId, Source), and ChapterPages had a FK pointing to a partial
        // key of Chapters. This migration recreates both tables with the correct constraints.
        // 2. Recreate the Chapters table without the redundant UNIQUE constraint and with an explicit FK for Comics
        await connection.ExecuteAsync(
            """
            CREATE TABLE Chapters_new (
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
                SortOrder INTEGER NOT NULL DEFAULT 0,
                IsAvailable INTEGER NOT NULL DEFAULT 1,
                PRIMARY KEY (Id, ComicId, Source),
                FOREIGN KEY (ComicId, Source) REFERENCES Comics(Id, Source) ON DELETE CASCADE
            );

            INSERT INTO Chapters_new
            SELECT Id, ComicId, Source, Title, Number, Volume,
                   Language, Groups, LastUpdate, Pages, SortOrder, IsAvailable
            FROM Chapters;
            """,
            transaction: transaction
        );

        // 3. Recreate ChapterPages, add the ComicId column, enhance PK uniqueness, and correct FK to point to the full primary key of Chapters
        await connection.ExecuteAsync(
            """
            CREATE TABLE ChapterPages_new (
                Id TEXT NOT NULL,
                ChapterId TEXT NOT NULL,
                ComicId TEXT NOT NULL,
                Source TEXT NOT NULL,
                PageNumber INTEGER NOT NULL,
                ImageUrl TEXT NOT NULL,
                PRIMARY KEY (ChapterId, ComicId, Source, Id),
                FOREIGN KEY (ChapterId, ComicId, Source)
                    REFERENCES Chapters(Id, ComicId, Source) ON DELETE CASCADE
            );

            INSERT INTO ChapterPages_new
            SELECT cp.Id, cp.ChapterId, ch.ComicId, cp.Source, cp.PageNumber, cp.ImageUrl
            FROM ChapterPages cp
            INNER JOIN Chapters ch ON cp.ChapterId = ch.Id AND cp.Source = ch.Source;
            """,
            transaction: transaction
        );

        // 4. Drop old Chapters and ChapterPages tables and rename the new ones
        await connection.ExecuteAsync(
            """
            DROP TABLE IF EXISTS Chapters;
            DROP TABLE IF EXISTS ChapterPages;

            ALTER TABLE Chapters_new RENAME TO Chapters;
            ALTER TABLE ChapterPages_new RENAME TO ChapterPages;
            """,
            transaction: transaction
        );

        // 5. Performance indexes
        await connection.ExecuteAsync(
            """
            CREATE INDEX IF NOT EXISTS IX_Chapters_ComicId_Source
                ON Chapters (ComicId, Source);

            CREATE INDEX IF NOT EXISTS IX_ChapterPages_Chapter
                ON ChapterPages (ChapterId, ComicId, Source);

            CREATE INDEX IF NOT EXISTS IX_ChapterUserData_ComicId_Source
                ON ChapterUserData (ComicId, Source);

            CREATE INDEX IF NOT EXISTS IX_ComicReadingProgress_ComicId_Source
                ON ComicReadingProgress (ComicId, Source);
            """,
            transaction: transaction
        );
    }
}
