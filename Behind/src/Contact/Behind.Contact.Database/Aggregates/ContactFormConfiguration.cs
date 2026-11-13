using Behind.Common.Database;
using Behind.Contact.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Contact.Database.Aggregates;

public sealed class ContactFormConfiguration : IEntityTypeConfiguration<ContactForm>
{
    public void Configure(EntityTypeBuilder<ContactForm> builder)
    {
        builder.ToTable("ContactForms");
        builder.HasVersionedKey();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Recipient).HasMaxLength(320).IsRequired();
        builder.Property(x => x.SubjectMode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.SubjectsJson).IsRequired();
        builder.Property(x => x.FieldsJson).IsRequired();
    }
}
