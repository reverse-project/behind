using Behind.Common.Database;
using Behind.Contact.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Contact.Database.Aggregates;

public sealed class ContactRequestConfiguration : IEntityTypeConfiguration<ContactRequest>
{
    public void Configure(EntityTypeBuilder<ContactRequest> builder)
    {
        builder.ToTable("ContactRequests");
        builder.HasVersionedKey();
        builder.Property(x => x.FormName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Recipient).HasMaxLength(320).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(10000).IsRequired();
        builder.Property(x => x.FieldsJson).IsRequired();
        builder.Property(x => x.DeliveryStatus).HasMaxLength(20).IsRequired();
    }
}
