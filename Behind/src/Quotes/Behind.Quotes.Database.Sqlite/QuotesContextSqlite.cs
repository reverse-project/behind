using Behind.Quotes.Database.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Behind.Quotes.Database.Sqlite;

public sealed class QuotesContextSqlite(DbContextOptions<QuotesContextSqlite> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QuoteConfiguration).Assembly);
}
