using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Behind.Core.Database.Sqlite;

public sealed class CoreContextSqliteFactory : IDesignTimeDbContextFactory<CoreContextSqlite>
{
    public CoreContextSqlite CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CoreContextSqlite>().UseSqlite("DataSource=:memory:").Options);
}

public sealed class VersioningContextSqliteFactory : IDesignTimeDbContextFactory<VersioningContextSqlite>
{
    public VersioningContextSqlite CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<VersioningContextSqlite>();
        builder.UseVersioningSqlite("DataSource=:memory:");
        return new(builder.Options);
    }
}
