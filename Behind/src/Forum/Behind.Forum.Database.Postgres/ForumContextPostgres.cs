using Behind.Forum.Database.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Behind.Forum.Database.Postgres;

public sealed class ForumContextPostgres(DbContextOptions<ForumContextPostgres> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ForumBoardConfiguration).Assembly);
}
