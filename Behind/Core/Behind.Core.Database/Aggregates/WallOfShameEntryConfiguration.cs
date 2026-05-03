using Behind.Core.Database.Converters;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class WallOfShameEntryConfiguration : IEntityTypeConfiguration<WallOfShameEntry>
{
    public void Configure(EntityTypeBuilder<WallOfShameEntry> builder)
    {
        builder.Property<Guid>("Id").ValueGeneratedOnAdd();
        builder.HasKey("Id");

        builder.Ignore(w => w.Expires);
        builder.Property<DateTimeOffset?>("ExpiresAt")
            .IsRequired(false);
        builder.HasIndex("ExpiresAt");

        builder.Property(w => w.Reason).HasConversion<ReasonConverter>().IsRequired();
        
        builder.Ignore(w => w.User);
        builder.Property<Guid>("UserId").IsRequired();
        builder.HasMany<User>()
            .WithOne()
            .HasForeignKey("UserId")
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Ignore(w => w.GivenBy);
        builder.Property<Guid>("GivenById").IsRequired();
        builder.HasMany<User>()
            .WithOne()
            .HasForeignKey("GivenById")
            .OnDelete(DeleteBehavior.Cascade);
    }
}