namespace Behind.Common.Database.Modules;

public interface IModuleMigrator
{
    string Module { get; }

    Task<IReadOnlyList<string>> PendingMigrationsAsync(CancellationToken cancellationToken = default);

    Task MigrateAsync(CancellationToken cancellationToken = default);
}
