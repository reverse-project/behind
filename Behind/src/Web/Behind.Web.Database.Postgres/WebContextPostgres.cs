using Behind.Common.Modules;
using Behind.Core.Database;
using Behind.Quotes.Database;
using Behind.Quotes.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Web.Database.Postgres;

public sealed class WebContextPostgres(DbContextOptions<WebContextPostgres> options, ModuleCatalog modules)
    : AbstractCoreContext(options, modules), IQuotesContext
{
    public DbSet<Quote> Quotes => Set<Quote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyCoreProviderConventions();
    }
}
