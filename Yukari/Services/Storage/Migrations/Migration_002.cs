using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Yukari.Services.Storage.Migrations;

internal class Migration_002 : IMigration
{
    public int Version => 2;
    public string Description => "Add Collections system";

    public async Task UpAsync(SqliteConnection connection, DbTransaction transaction)
    {
        await connection.ExecuteAsync(
            """
            CREATE TABLE IF NOT EXISTS Collections (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
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
    }
}
