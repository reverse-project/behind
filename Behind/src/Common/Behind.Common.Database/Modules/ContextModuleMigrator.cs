using Microsoft.EntityFrameworkCore;

namespace Behind.Common.Database.Modules;

public sealed class ContextModuleMigrator<TContext>(string module, TContext context) : IModuleMigrator
    where TContext : DbContext
{
    public string Module => module;

    public async Task<IReadOnlyList<string>> PendingMigrationsAsync(CancellationToken cancellationToken = default) =>
        (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

    public Task MigrateAsync(CancellationToken cancellationToken = default) =>
        context.Database.MigrateAsync(cancellationToken);
}
