using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Yukari.Services.Storage.Migrations;

internal class Migration_007 : IMigration
{
    public int Version => 7;
    public string Description =>
        "Add IsConfigurable column to ComicSources and create ComicSourceSettings, ComicSourceCredentials, and CredentialProtection tables";

    public async Task UpAsync(SqliteConnection connection, DbTransaction transaction)
    {
        await connection.ExecuteAsync(
            """
            ALTER TABLE ComicSources ADD COLUMN IsConfigurable INTEGER NOT NULL DEFAULT 0;

            CREATE TABLE ComicSourceSettings (
                SourceName TEXT NOT NULL,
                SettingKey TEXT NOT NULL,
                SettingValue TEXT NOT NULL,
                PRIMARY KEY (SourceName, SettingKey),
                FOREIGN KEY (SourceName) REFERENCES ComicSources(Name) ON DELETE CASCADE
            );

            CREATE TABLE ComicSourceCredentials (
                SourceName TEXT NOT NULL,
                SettingKey TEXT NOT NULL,
                Ciphertext BLOB NOT NULL,
                PRIMARY KEY (SourceName, SettingKey),
                FOREIGN KEY (SourceName) REFERENCES ComicSources(Name) ON DELETE CASCADE
            );

            CREATE TABLE CredentialProtection (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                Salt BLOB NOT NULL,
                Canary BLOB NOT NULL,
                Iterations INTEGER NOT NULL
            );
            """,
            transaction: transaction
        );
    }
}
