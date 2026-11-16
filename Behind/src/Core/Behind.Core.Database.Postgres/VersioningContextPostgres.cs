using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Postgres;

public sealed class VersioningContextPostgres(DbContextOptions<VersioningContextPostgres> options)
    : AbstractVersioningContext(options)
{
    public const string MigrationsHistoryTable = "__VersioningMigrationsHistory";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyVersioningProviderConventions();
    }
}
