using Behind.Common.Database.Modules;
using Behind.Common.Modules;
using Behind.Contact.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Behind.Contact.Database.Postgres;

public sealed class ContactContextPostgresFactory : IDesignTimeDbContextFactory<ContactContextPostgres>
{
    private const string DesignTimeConnection = "Host=localhost;Database=behind";

    public ContactContextPostgres CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ContactContextPostgres>()
            .UseNpgsql(
                DesignTimeConnection,
                postgres => postgres.MigrationsHistoryTable(
                    new ModuleDescriptor(ContactModuleNames.Key, ContactModuleNames.Name).MigrationsHistoryTable))
            .Options);
}
