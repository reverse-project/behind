using Behind.Core.Database.Converters;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class MagicLinkConfiguration : IEntityTypeConfiguration<MagicLink>
{
    public const string ConsumedAtProperty = "ConsumedAtValue";

    public void Configure(EntityTypeBuilder<MagicLink> builder)
    {
        builder.ToTable("MagicLinks");

        builder.Property<Guid>("Id").ValueGeneratedOnAdd();
        builder.HasKey("Id");

        builder.Property(l => l.Nonce).HasConversion(n => n.Nonce, g => new TokenNonce(g)).IsRequired();
        builder.HasIndex(l => l.Nonce).IsUnique();

        builder.Property(l => l.IssuedAt).HasConversion<DateTimeOffsetConverter>();
        builder.Property(l => l.ExpiresAt).HasConversion<DateTimeOffsetConverter>();
        builder.Ignore(l => l.ConsumedAt);
        builder.Property<DateTimeOffset?>(ConsumedAtProperty)
            .HasColumnName(nameof(MagicLink.ConsumedAt))
            .HasConversion<DateTimeOffsetConverter>()
            .IsRequired(false);

        builder.Property<Guid>("UserId").IsRequired();
        builder.HasOne<HumanUser>()
            .WithMany()
            .HasForeignKey("UserId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex("UserId");
    }
}
