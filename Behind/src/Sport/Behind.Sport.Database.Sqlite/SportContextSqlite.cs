using Behind.Sport.Database.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Behind.Sport.Database.Sqlite;

public sealed class SportContextSqlite(DbContextOptions<SportContextSqlite> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DisciplineConfiguration).Assembly);
}
