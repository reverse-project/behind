using Behind.Quotes.Database.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Behind.Quotes.Database.Postgres;

public sealed class QuotesContextPostgres(DbContextOptions<QuotesContextPostgres> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QuoteConfiguration).Assembly);
}
