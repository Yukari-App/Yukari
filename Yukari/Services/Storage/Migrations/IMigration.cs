using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Yukari.Services.Storage.Migrations;

public interface IMigration
{
    int Version { get; }
    string Description { get; }

    Task UpAsync(SqliteConnection connection, DbTransaction transaction);
}
