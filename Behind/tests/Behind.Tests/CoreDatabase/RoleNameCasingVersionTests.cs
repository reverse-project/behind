using Behind.Common;
using Behind.Core.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class RoleNameCasingVersionTests
{
    [Fact]
    public void Register_RebuildsOldKeysBeforeAllowingWrites()
    {
        using var connection = LegacyDatabase("équipe");
        OrdinalRoleNameConnectionInterceptor.Register(connection);
        Assert.Equal(OrdinalIgnoreCaseKey.Version, Scalar(connection,
            "SELECT Version FROM __BehindRoleNameCasing"));
        Assert.Throws<SqliteException>(() => Execute(connection,
            "INSERT INTO Roles(Name) VALUES ('ÉQUIPE')"));
        Assert.Equal("ok", Scalar(connection, "PRAGMA integrity_check"));
        OrdinalRoleNameConnectionInterceptor.Register(connection);
        Assert.Equal(1L, Scalar(connection, "SELECT count(*) FROM Roles"));
    }

    [Fact]
    public void Register_ConflictingNewKeysAbortAndPreserveRowsAndVersion()
    {
        var path = Path.Combine(Path.GetTempPath(), $"behind-conflicting-casing-{Guid.NewGuid():N}.db");
        try
        {
            using var connection = LegacyDatabaseAt($"Data Source={path};Pooling=False", "équipe", "ÉQUIPE");
            Assert.Throws<SqliteException>(() => OrdinalRoleNameConnectionInterceptor.Register(connection));
            Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
            connection.Open();
            connection.CreateFunction<string, string>("BehindOrdinalIgnoreCaseKey", value => value, isDeterministic: true);
            Assert.Equal("old-runtime", Scalar(connection, "SELECT Version FROM __BehindRoleNameCasing"));
            Assert.Equal(2L, Scalar(connection, "SELECT count(*) FROM Roles NOT INDEXED"));
            Assert.Equal("ok", Scalar(connection, "PRAGMA integrity_check"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ClosedConnection_IsCheckedWhenEfOpensIt()
    {
        var path = Path.Combine(Path.GetTempPath(), $"behind-casing-{Guid.NewGuid():N}.db");
        try
        {
            using (var initial = LegacyDatabaseAt($"Data Source={path};Pooling=False", "équipe"))
            {
            }

            await using var context = new CoreContextSqlite(new DbContextOptionsBuilder<CoreContextSqlite>()
                .UseSqlite($"Data Source={path};Pooling=False").Options);
            await Assert.ThrowsAsync<SqliteException>(() => context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Roles(Name) VALUES ('ÉQUIPE')"));
            await context.Database.OpenConnectionAsync();
            Assert.Equal(OrdinalIgnoreCaseKey.Version, Scalar(
                (SqliteConnection)context.Database.GetDbConnection(), "SELECT Version FROM __BehindRoleNameCasing"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Register_CurrentVersionInsideCallerTransactionPreservesIt()
    {
        using var connection = LegacyDatabase("équipe");
        OrdinalRoleNameConnectionInterceptor.Register(connection);
        using var transaction = connection.BeginTransaction();
        OrdinalRoleNameConnectionInterceptor.Register(connection);
        transaction.Commit();
        Assert.Equal("ok", Scalar(connection, "PRAGMA integrity_check"));
    }

    [Fact]
    public void MissingVersion_RebuildsAndRecordsCurrentVersion()
    {
        using var connection = LegacyDatabase("équipe");
        Execute(connection, "DROP TABLE __BehindRoleNameCasing");
        OrdinalRoleNameConnectionInterceptor.Register(connection);
        Assert.Equal(OrdinalIgnoreCaseKey.Version, Scalar(connection,
            "SELECT Version FROM __BehindRoleNameCasing"));
        Assert.Throws<SqliteException>(() => Execute(connection,
            "INSERT INTO Roles(Name) VALUES ('ÉQUIPE')"));
    }

    private static SqliteConnection LegacyDatabase(params string[] names) =>
        LegacyDatabaseAt("Data Source=:memory:", names);

    private static SqliteConnection LegacyDatabaseAt(string connectionString, params string[] names)
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        connection.CreateFunction<string, string>("BehindOrdinalIgnoreCaseKey", value => value, isDeterministic: true);
        Execute(connection, """
            CREATE TABLE Roles (Name TEXT NOT NULL, NormalizedName TEXT COLLATE BINARY
                GENERATED ALWAYS AS (BehindOrdinalIgnoreCaseKey(Name)) VIRTUAL);
            CREATE UNIQUE INDEX UX_Roles_NormalizedName ON Roles(NormalizedName);
            CREATE TABLE __BehindRoleNameCasing(Id INTEGER PRIMARY KEY, Version TEXT NOT NULL);
            INSERT INTO __BehindRoleNameCasing VALUES(1, 'old-runtime');
            """);
        foreach (var name in names)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Roles(Name) VALUES ($name)";
            command.Parameters.AddWithValue("$name", name);
            command.ExecuteNonQuery();
        }

        return connection;
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
