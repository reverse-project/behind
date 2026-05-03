using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class EmailConfiguration : IEntityTypeConfiguration<Email>
{
    public void Configure(EntityTypeBuilder<Email> builder)
    {
        builder.Property<Guid>("Id").ValueGeneratedOnAdd();
        builder.HasKey("Id");

        builder.Property(e => e.Value).HasMaxLength(255).IsRequired();

        builder.Property<Guid>("UserId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey("UserId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}