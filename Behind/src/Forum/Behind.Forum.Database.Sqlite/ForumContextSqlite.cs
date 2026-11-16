using Behind.Forum.Database.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Behind.Forum.Database.Sqlite;

public sealed class ForumContextSqlite(DbContextOptions<ForumContextSqlite> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ForumBoardConfiguration).Assembly);
}
