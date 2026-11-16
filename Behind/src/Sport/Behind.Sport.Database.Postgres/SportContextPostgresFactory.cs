using Behind.Common.Database.Modules;
using Behind.Common.Modules;
using Behind.Sport.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Behind.Sport.Database.Postgres;

public sealed class SportContextPostgresFactory : IDesignTimeDbContextFactory<SportContextPostgres>
{
    private const string DesignTimeConnection = "Host=localhost;Database=behind";

    public SportContextPostgres CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SportContextPostgres>()
            .UseNpgsql(
                DesignTimeConnection,
                postgres => postgres.MigrationsHistoryTable(
                    new ModuleDescriptor(SportModuleNames.Key, SportModuleNames.Name).MigrationsHistoryTable))
            .Options);
}
