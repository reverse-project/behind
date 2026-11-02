using Behind.Common.Database;
using Behind.Core.Database.Converters;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class NewsConfiguration : IEntityTypeConfiguration<News>
{
    public const string AuthorIdProperty = "AuthorId";
    public const string CategoryIdProperty = "CategoryId";
    public const string HighlightedProperty = nameof(News.Highlighted);
    public const string StartAtProperty = "StartAtUtc";
    public const string EndAtProperty = "EndAtUtc";

    public void Configure(EntityTypeBuilder<News> builder)
    {
        builder.ToTable("News");
        builder.HasVersionedKey();

        builder.Property(news => news.Title).HasMaxLength(News.MaxTitleLength).IsRequired();
        builder.Property(news => news.Summary).IsRequired();
        builder.Property(news => news.Content).IsRequired();
        builder.Ignore(news => news.StartAt);
        builder.Property<DateTimeOffset?>(StartAtProperty)
            .HasConversion<DateTimeOffsetConverter>()
            .IsRequired(false);
        builder.Ignore(news => news.EndAt);
        builder.Property<DateTimeOffset?>(EndAtProperty)
            .HasConversion<DateTimeOffsetConverter>()
            .IsRequired(false);

        builder.Property<Guid>(AuthorIdProperty).IsRequired();
        builder.HasOne<HumanUser>()
            .WithMany()
            .HasForeignKey(AuthorIdProperty)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property<Guid?>(CategoryIdProperty).IsRequired(false);

        builder.HasIndex(CategoryIdProperty);
        builder.HasIndex(StartAtProperty);
    }
}
