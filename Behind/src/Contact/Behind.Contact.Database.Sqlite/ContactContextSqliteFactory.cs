using Behind.Common.Database.Modules;
using Behind.Common.Modules;
using Behind.Contact.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Behind.Contact.Database.Sqlite;

public sealed class ContactContextSqliteFactory : IDesignTimeDbContextFactory<ContactContextSqlite>
{
    private const string DesignTimeConnection = "Data Source=contact-design.db";

    public ContactContextSqlite CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ContactContextSqlite>()
            .UseSqlite(
                DesignTimeConnection,
                sqlite => sqlite.MigrationsHistoryTable(
                    new ModuleDescriptor(ContactModuleNames.Key, ContactModuleNames.Name).MigrationsHistoryTable))
            .Options);
}
