using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class ContentAgeOptionConfiguration : IEntityTypeConfiguration<ContentAgeOption>
{
    public void Configure(EntityTypeBuilder<ContentAgeOption> builder)
    {
        builder.ToTable("ContentAgeOptions");
        builder.HasVersionedKey();
        builder.Property(item => item.Age).IsRequired();
        builder.Property(item => item.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(item => item.Age);
    }
}
