using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Yukari.Services.Storage.Migrations;

internal class Migration_003 : IMigration
{
    public int Version => 3;
    public string Description => "Add Status column to Comics table";

    public async Task UpAsync(SqliteConnection connection, DbTransaction transaction)
    {
        // Status is stored as TEXT and mapped to ComicStatus enum in the app.
        // Default 'unknown' ensures existing favorited comics get a valid value without a data migration.
        await connection.ExecuteAsync(
            "ALTER TABLE Comics ADD COLUMN Status TEXT NOT NULL DEFAULT 'unknown';",
            transaction: transaction
        );
    }
}
