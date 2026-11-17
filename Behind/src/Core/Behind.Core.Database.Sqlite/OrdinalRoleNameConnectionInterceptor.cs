using System.Data.Common;
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
        Register(connection);
        return Task.CompletedTask;
    }

    public static void Register(DbConnection connection)
    {
        if (connection is SqliteConnection sqlite)
        {
            sqlite.CreateFunction<string?, string?>("BehindOrdinalIgnoreCaseKey",
                value => value is null ? null : OrdinalIgnoreCaseKey.Create(value), isDeterministic: true);
        }
    }
}
