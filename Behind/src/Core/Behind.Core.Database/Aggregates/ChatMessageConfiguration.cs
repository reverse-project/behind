using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");
        builder.HasVersionedKey();
        builder.Property(message => message.AuthorId).IsRequired();
        builder.Property(message => message.Text).HasMaxLength(ChatMessage.MaxTextLength).IsRequired();
        builder.Property(message => message.IsAnonymous).HasDefaultValue(false);
        builder.HasIndex(message => message.AuthorId);
        builder.HasIndex(message => message.ReplyToId);
    }
}
