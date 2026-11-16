using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class ContentRestrictionConfiguration : IEntityTypeConfiguration<ContentRestriction>
{
    public void Configure(EntityTypeBuilder<ContentRestriction> builder)
    {
        builder.ToTable("ContentRestrictions");
        builder.HasVersionedKey();
        builder.Property(item => item.EntityType).HasMaxLength(200).IsRequired();
        builder.Property(item => item.EntityId).IsRequired();
        builder.Property(item => item.MinimumAge).IsRequired();
        builder.HasIndex(item => new { item.EntityType, item.EntityId });
    }
}
