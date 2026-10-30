using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class UserAgentConfiguration : IEntityTypeConfiguration<UserAgent>
{
    public void Configure(EntityTypeBuilder<UserAgent> builder)
    {
        builder.ToTable("UserAgents");

        builder.Property<Guid>("Id").ValueGeneratedOnAdd();
        builder.HasKey("Id");

        builder.Property(a => a.Value).HasMaxLength(UserAgent.MaxLength).IsRequired();
        builder.HasIndex(a => a.Value).IsUnique();
    }
}
