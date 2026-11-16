using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class PrivateMessageEmailDeliveryConfiguration : IEntityTypeConfiguration<PrivateMessageEmailDelivery>
{
    public void Configure(EntityTypeBuilder<PrivateMessageEmailDelivery> builder)
    {
        builder.ToTable("PrivateMessageEmailDeliveries");
        builder.HasVersionedKey();
        builder.Property(delivery => delivery.Email).HasMaxLength(320).IsRequired();
        builder.Property(delivery => delivery.Status).HasMaxLength(16).IsRequired();
        builder.HasIndex(delivery => delivery.MessageId);
        builder.HasIndex(delivery => delivery.Status);
    }
}
