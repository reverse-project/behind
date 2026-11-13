using Behind.Contact.Database.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Behind.Contact.Database.Postgres;

public sealed class ContactContextPostgres(DbContextOptions<ContactContextPostgres> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContactFormConfiguration).Assembly);
}
