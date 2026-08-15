using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Yukari.Services.Storage.Migrations;

internal class Migration_006 : IMigration
{
    public int Version => 6;
    public string Description => "Add LastUpdate column to Comics table";

    public async Task UpAsync(SqliteConnection connection, DbTransaction transaction)
    {
        await connection.ExecuteAsync(
            "ALTER TABLE Comics ADD COLUMN LastUpdate TEXT;",
            transaction: transaction
        );
    }
}
