using Behind.Common.Database;
using Behind.Quotes.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Quotes.Database.Aggregates;

public sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public const string TableName = "Quotes";

    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable(TableName);
        builder.HasVersionedKey();

        builder.Property(quote => quote.Text).HasMaxLength(Quote.MaxTextLength).IsRequired();
        builder.Property(quote => quote.Author).HasMaxLength(Quote.MaxAuthorLength).IsRequired();

        builder.HasIndex(quote => quote.Author);
    }
}
