using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Postgres;

public sealed class CoreContextPostgres(DbContextOptions<CoreContextPostgres> options) : AbstractCoreContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyCoreProviderConventions();
    }
}
