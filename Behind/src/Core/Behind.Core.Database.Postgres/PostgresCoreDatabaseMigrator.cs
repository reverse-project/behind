using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Postgres;

public sealed class PostgresCoreDatabaseMigrator(CoreContextPostgres context) : ICoreDatabaseMigrator
{
    public async Task<IReadOnlyList<string>> PendingMigrationsAsync(CancellationToken cancellationToken = default) =>
        (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

    public Task MigrateAsync(CancellationToken cancellationToken = default) =>
        context.Database.MigrateAsync(cancellationToken);
}
