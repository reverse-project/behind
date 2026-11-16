using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Common.Database;

public sealed class ActiveVersionConfiguration : IEntityTypeConfiguration<ActiveVersion>
{
    public const int MaxKeyLength = 100;

    public void Configure(EntityTypeBuilder<ActiveVersion> builder)
    {
        builder.Property(active => active.EntityType).HasMaxLength(MaxKeyLength).IsRequired();
        builder.Property(active => active.EntityKey).HasMaxLength(MaxKeyLength).IsRequired();
        builder.Property(active => active.Version).ValueGeneratedNever();
        builder.HasKey(active => new { active.EntityType, active.EntityKey });
    }
}
