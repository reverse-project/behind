using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Behind.Forum.Database.Sqlite;

public sealed class ForumContextSqliteFactory : IDesignTimeDbContextFactory<ForumContextSqlite>
{
    public ForumContextSqlite CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ForumContextSqlite>()
            .UseSqlite("Data Source=forum-design.db", sqlite => sqlite.MigrationsHistoryTable("__forumMigrationsHistory"))
            .Options);
}
