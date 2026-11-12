using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class ContentLocaleConfiguration : IEntityTypeConfiguration<ContentLocale>
{
    public void Configure(EntityTypeBuilder<ContentLocale> builder)
    {
        builder.ToTable("ContentLocales");
        builder.HasVersionedKey();
        builder.Property(item => item.EntityType).HasMaxLength(200).IsRequired();
        builder.Property(item => item.EntityId).IsRequired();
        builder.Property(item => item.Locale).HasMaxLength(100);
        builder.HasIndex(item => new { item.EntityType, item.EntityId, item.Locale });
        builder.HasIndex(nameof(ContentLocale.EntityType), nameof(ContentLocale.EntityId), VersionedColumns.Version).IsUnique();
    }
}
