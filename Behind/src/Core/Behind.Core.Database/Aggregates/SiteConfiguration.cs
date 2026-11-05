using Behind.Core.Database.Converters;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public const string SiteIdProperty = "SiteId";
    public const string VersionProperty = "Version";
    public const string PublishedAtProperty = "PublishedAtUtc";

    public void Configure(EntityTypeBuilder<Site> builder)
    {
        builder.Ignore(site => site.Id);
        builder.Property<Guid>(SiteIdProperty).ValueGeneratedNever();
        builder.Property<long>(VersionProperty).ValueGeneratedNever();
        builder.HasKey(SiteIdProperty, VersionProperty);

        builder.Property(site => site.Title).HasMaxLength(Site.MaxTitleLength).IsRequired();
        builder.Property(site => site.Slug).HasMaxLength(Site.MaxSlugLength).IsRequired();
        builder.Property(site => site.Content).IsRequired();
        builder.Property(site => site.HideTitle).HasDefaultValue(false);
        builder.Property(site => site.CreatedAt).HasConversion<DateTimeOffsetConverter>();

        builder.Ignore(site => site.PublishedAt);
        builder.Property<DateTimeOffset?>(PublishedAtProperty)
            .HasConversion<DateTimeOffsetConverter>()
            .IsRequired(false);

        builder.HasIndex(site => site.Slug);
    }
}
