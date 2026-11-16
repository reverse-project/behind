using Behind.Common.Database.Modules;
using Behind.Common.Modules;
using Behind.Sport.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Behind.Sport.Database.Sqlite;

public sealed class SportContextSqliteFactory : IDesignTimeDbContextFactory<SportContextSqlite>
{
    private const string DesignTimeConnection = "Data Source=sport-design.db";

    public SportContextSqlite CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SportContextSqlite>()
            .UseSqlite(
                DesignTimeConnection,
                sqlite => sqlite.MigrationsHistoryTable(
                    new ModuleDescriptor(SportModuleNames.Key, SportModuleNames.Name).MigrationsHistoryTable))
            .Options);
}
