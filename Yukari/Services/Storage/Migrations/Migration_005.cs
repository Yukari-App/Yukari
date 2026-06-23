using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Yukari.Services.Storage.Migrations;

internal class Migration_005 : IMigration
{
    public int Version => 5;
    public string Description => "Add ReleasesPage column to ComicSources table";

    public async Task UpAsync(SqliteConnection connection, DbTransaction transaction)
    {
        await connection.ExecuteAsync(
            "ALTER TABLE ComicSources ADD COLUMN ReleasesPage TEXT;",
            transaction: transaction
        );
    }
}
