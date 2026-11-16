using Behind.Contact.Database.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Behind.Contact.Database.Sqlite;

public sealed class ContactContextSqlite(DbContextOptions<ContactContextSqlite> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContactFormConfiguration).Assembly);
}
