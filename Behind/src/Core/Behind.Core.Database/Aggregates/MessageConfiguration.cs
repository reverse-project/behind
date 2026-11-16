using Behind.Common.Database;
using Behind.Core.Database.Converters;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");
        builder.HasVersionedKey();

        builder.Property(message => message.Name).HasMaxLength(Message.MaxNameLength).IsRequired();
        builder.Property(message => message.Title).HasMaxLength(Message.MaxTitleLength).IsRequired();
        builder.Property(message => message.Content).HasMaxLength(Message.MaxContentLength).IsRequired();
        builder.Property(message => message.Level)
            .HasConversion<string>()
            .HasMaxLength(Message.MaxLevelLength)
            .IsRequired();
        builder.Property(message => message.StartsAt)
            .HasConversion<DateTimeOffsetConverter>()
            .IsRequired(false);
        builder.Property(message => message.EndsAt)
            .HasConversion<DateTimeOffsetConverter>()
            .IsRequired(false);

        builder.HasIndex(message => message.Name);
    }
}
