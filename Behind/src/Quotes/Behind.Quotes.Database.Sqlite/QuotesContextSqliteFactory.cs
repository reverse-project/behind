using Behind.Common.Database.Modules;
using Behind.Common.Modules;
using Behind.Quotes.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Behind.Quotes.Database.Sqlite;

public sealed class QuotesContextSqliteFactory : IDesignTimeDbContextFactory<QuotesContextSqlite>
{
    private const string DesignTimeConnection = "Data Source=quotes-design.db";

    public QuotesContextSqlite CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<QuotesContextSqlite>()
            .UseSqlite(
                DesignTimeConnection,
                sqlite => sqlite.MigrationsHistoryTable(
                    new ModuleDescriptor(QuotesModuleNames.Key, QuotesModuleNames.Name).MigrationsHistoryTable))
            .Options);
}
