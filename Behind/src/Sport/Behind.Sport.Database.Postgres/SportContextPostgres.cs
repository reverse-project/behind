using Behind.Sport.Database.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Behind.Sport.Database.Postgres;

public sealed class SportContextPostgres(DbContextOptions<SportContextPostgres> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DisciplineConfiguration).Assembly);
}
