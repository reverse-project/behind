using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Behind.Forum.Database.Postgres;

public sealed class ForumContextPostgresFactory : IDesignTimeDbContextFactory<ForumContextPostgres>
{
    public ForumContextPostgres CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ForumContextPostgres>()
            .UseNpgsql("Host=localhost;Database=forum;Username=postgres",
                postgres => postgres.MigrationsHistoryTable("__forumMigrationsHistory"))
            .Options);
}
