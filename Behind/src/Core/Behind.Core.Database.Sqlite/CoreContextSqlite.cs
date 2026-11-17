using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Sqlite;

public sealed class CoreContextSqlite(DbContextOptions<CoreContextSqlite> options) : AbstractCoreContext(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureOrdinalRoleNames();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplySqliteCoreProviderConventions();
    }
}
