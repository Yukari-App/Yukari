using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Yukari.Services.Storage.Migrations;

namespace Yukari.Services.Storage;

internal class DatabaseMigrator
{
    private readonly string _connectionString;

    private static readonly IReadOnlyList<IMigration> AllMigrations = [new Migration_001()];

    public DatabaseMigrator(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task MigrateAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        int currentVersion = await GetCurrentVersionAsync(connection);
        int targetVersion = AllMigrations.Max(m => m.Version);

        if (currentVersion == targetVersion)
            return;

        if (currentVersion > targetVersion)
            throw new InvalidOperationException(
                $"The database is in version {currentVersion}, but the app supports up to version {targetVersion}. "
                    + "Please update the application."
            );

        var pending = AllMigrations
            .Where(m => m.Version > currentVersion)
            .OrderBy(m => m.Version)
            .ToList();

        foreach (var migration in pending)
        {
            await ApplyMigrationAsync(connection, migration);
        }
    }

    private async Task ApplyMigrationAsync(SqliteConnection connection, IMigration migration)
    {
        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await migration.UpAsync(connection, transaction);

            await connection.ExecuteAsync(
                $"PRAGMA user_version = {migration.Version};",
                transaction: transaction
            );

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            throw new InvalidOperationException(
                $"Migration {migration.Version} ({migration.Description}) failed: {ex.Message}",
                ex
            );
        }
    }

    private static async Task<int> GetCurrentVersionAsync(SqliteConnection connection)
    {
        var result = await connection.QueryFirstAsync<int>("PRAGMA user_version;");
        return result;
    }
}
