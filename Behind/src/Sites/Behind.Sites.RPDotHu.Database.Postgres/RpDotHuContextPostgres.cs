
using Behind.Common.Modules;
using Behind.Contact.Database;
using Behind.Contact.Domain;
using Behind.Core.Database;
using Microsoft.EntityFrameworkCore;

namespace Behind.Sites.RPDotHu.Database.Postgres;

public sealed class RpDotHuContextPostgres(DbContextOptions<RpDotHuContextPostgres> options, ModuleCatalog modules)
    : AbstractCoreContext(options, modules), IContactContext
{
    public DbSet<ContactForm> ContactForms => Set<ContactForm>();

    public DbSet<ContactRequest> ContactRequests => Set<ContactRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (!Modules.Modules.Any(module => module.Key == "contact"))
        {
            modelBuilder.Ignore<ContactForm>();
            modelBuilder.Ignore<ContactRequest>();
        }

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyCoreProviderConventions();
    }
}
