using Behind.Common.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Core.Database.Sqlite;

public sealed class SqliteCoreDatabaseMigrator(CoreContextSqlite context, IServiceProvider services)
    : ICoreDatabaseMigrator
{
    public async Task<IReadOnlyList<string>> PendingMigrationsAsync(CancellationToken cancellationToken = default) =>
        (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

    public Task MigrateAsync(CancellationToken cancellationToken = default) =>
        context.MigrateWithVfsAsync(services.GetRequiredService<IContentStore>(), cancellationToken);
}
