namespace Behind.Core.Database;

public interface ICoreDatabaseMigrator
{
    Task<IReadOnlyList<string>> PendingMigrationsAsync(CancellationToken cancellationToken = default);

    Task MigrateAsync(CancellationToken cancellationToken = default);
}
