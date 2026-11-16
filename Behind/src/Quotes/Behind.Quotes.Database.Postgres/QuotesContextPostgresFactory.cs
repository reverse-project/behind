using Behind.Common.Database.Modules;
using Behind.Common.Modules;
using Behind.Quotes.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Behind.Quotes.Database.Postgres;

public sealed class QuotesContextPostgresFactory : IDesignTimeDbContextFactory<QuotesContextPostgres>
{
    private const string DesignTimeConnection = "Host=localhost;Database=behind";

    public QuotesContextPostgres CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<QuotesContextPostgres>()
            .UseNpgsql(
                DesignTimeConnection,
                postgres => postgres.MigrationsHistoryTable(
                    new ModuleDescriptor(QuotesModuleNames.Key, QuotesModuleNames.Name).MigrationsHistoryTable))
            .Options);
}
