using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Yukari.Services.Storage.Migrations;

internal class Migration_004 : IMigration
{
    public int Version => 4;
    public string Description => "Add LastReadAt column to ComicUserData table";

    public async Task UpAsync(SqliteConnection connection, DbTransaction transaction)
    {
        // Required to sort by “Last Read” in GetFavoriteComicsAsync.
        // Always use `datetime(‘now’)` when updating this column
        await connection.ExecuteAsync(
            "ALTER TABLE ComicUserData ADD COLUMN LastReadAt TEXT;",
            transaction: transaction
        );
    }
}
