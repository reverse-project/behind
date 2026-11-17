using System.Data.Common;
using System.Data;
using Behind.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Behind.Core.Database.Sqlite;

public sealed class OrdinalRoleNameConnectionInterceptor : DbConnectionInterceptor
{
    public static readonly OrdinalRoleNameConnectionInterceptor Instance = new();

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) => Register(connection);

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Register(connection);
        return Task.CompletedTask;
    }

    public static void Register(DbConnection connection)
    {
        if (connection is SqliteConnection sqlite)
        {
            sqlite.CreateFunction<string?, string?>("BehindOrdinalIgnoreCaseKey",
                value => value is null ? null : OrdinalIgnoreCaseKey.Create(value), isDeterministic: true);
            if (sqlite.State == ConnectionState.Open)
            {
                try
                {
                    EnsureCasingVersion(sqlite);
                }
                catch
                {
                    sqlite.Close();
                    throw;
                }
            }
        }
    }

    private static void EnsureCasingVersion(SqliteConnection connection)
    {
        using var schema = connection.CreateCommand();
        schema.CommandText = """
            SELECT count(*) FROM sqlite_schema
            WHERE type = 'table' AND name = 'Roles' AND sql LIKE '%BehindOrdinalIgnoreCaseKey%'
            AND EXISTS (SELECT 1 FROM sqlite_schema WHERE type = 'index' AND name = 'UX_Roles_NormalizedName')
            """;
        if (Convert.ToInt64(schema.ExecuteScalar()) == 0)
        {
            return;
        }

        schema.CommandText = "SELECT count(*) FROM sqlite_schema WHERE type = 'table' AND name = '__BehindRoleNameCasing'";
        if (Convert.ToInt64(schema.ExecuteScalar()) != 0)
        {
            schema.CommandText = "SELECT Version FROM __BehindRoleNameCasing WHERE Id = 1";
            if (string.Equals(schema.ExecuteScalar() as string, OrdinalIgnoreCaseKey.Version, StringComparison.Ordinal))
            {
                return;
            }
        }

        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS "__BehindRoleNameCasing" (
                "Id" INTEGER NOT NULL PRIMARY KEY CHECK ("Id" = 1), "Version" TEXT NOT NULL);
            SELECT "Version" FROM "__BehindRoleNameCasing" WHERE "Id" = 1;
            """;
        var previous = command.ExecuteScalar() as string;
        if (!string.Equals(previous, OrdinalIgnoreCaseKey.Version, StringComparison.Ordinal))
        {
            command.CommandText = "REINDEX \"UX_Roles_NormalizedName\"";
            command.ExecuteNonQuery();
            command.CommandText = """
                INSERT INTO "__BehindRoleNameCasing" ("Id", "Version") VALUES (1, $version)
                ON CONFLICT ("Id") DO UPDATE SET "Version" = excluded."Version";
                """;
            command.Parameters.AddWithValue("$version", OrdinalIgnoreCaseKey.Version);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }
}
