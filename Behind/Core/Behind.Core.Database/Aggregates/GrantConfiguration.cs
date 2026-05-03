using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class GrantConfiguration : IEntityTypeConfiguration<Grant>
{
    public void Configure(EntityTypeBuilder<Grant> builder)
    {
        builder.Property<Guid>("Id").ValueGeneratedOnAdd();
        builder.HasKey("Id");

        builder.Property(g => g.Name).HasMaxLength(255).IsRequired();
        builder.HasAlternateKey(g => g.Name);
    }
}