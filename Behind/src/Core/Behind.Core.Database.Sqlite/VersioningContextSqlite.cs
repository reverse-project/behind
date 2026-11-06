using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Sqlite;

public sealed class VersioningContextSqlite(DbContextOptions<VersioningContextSqlite> options)
    : AbstractVersioningContext(options)
{
    public const string MigrationsHistoryTable = "__VersioningMigrationsHistory";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyVersioningProviderConventions();
    }
}
