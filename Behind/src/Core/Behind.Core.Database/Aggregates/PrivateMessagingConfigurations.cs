using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class PrivateConversationConfiguration : IEntityTypeConfiguration<PrivateConversation>
{
    public void Configure(EntityTypeBuilder<PrivateConversation> builder)
    {
        builder.ToTable("PrivateConversations");
        builder.HasVersionedKey();
        builder.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.SenderId);
    }
}

public sealed class PrivateConversationRecipientConfiguration : IEntityTypeConfiguration<PrivateConversationRecipient>
{
    public void Configure(EntityTypeBuilder<PrivateConversationRecipient> builder)
    {
        builder.ToTable("PrivateConversationRecipients");
        builder.HasVersionedKey();
        builder.HasIndex(nameof(PrivateConversationRecipient.ConversationId), nameof(PrivateConversationRecipient.RecipientId),
            nameof(PrivateConversationRecipient.IsRole), VersionedColumns.Version).IsUnique();
        builder.HasIndex(x => x.RecipientId);
    }
}

public sealed class PrivateMessageConfiguration : IEntityTypeConfiguration<PrivateMessage>
{
    public void Configure(EntityTypeBuilder<PrivateMessage> builder)
    {
        builder.ToTable("PrivateMessages");
        builder.HasVersionedKey();
        builder.Property(x => x.Content).HasMaxLength(100000).IsRequired();
        builder.HasIndex(x => x.ConversationId);
        builder.HasIndex(x => x.ReplyToId);
    }
}

public sealed class PrivateMailboxConfiguration : IEntityTypeConfiguration<PrivateMailbox>
{
    public void Configure(EntityTypeBuilder<PrivateMailbox> builder)
    {
        builder.ToTable("PrivateMailboxes");
        builder.HasVersionedKey();
        builder.HasIndex(nameof(PrivateMailbox.ConversationId), nameof(PrivateMailbox.UserId), VersionedColumns.Version).IsUnique();
    }
}

public sealed class PrivateMessageReadConfiguration : IEntityTypeConfiguration<PrivateMessageRead>
{
    public void Configure(EntityTypeBuilder<PrivateMessageRead> builder)
    {
        builder.ToTable("PrivateMessageReads");
        builder.HasVersionedKey();
        builder.HasIndex(nameof(PrivateMessageRead.MessageId), nameof(PrivateMessageRead.UserId), VersionedColumns.Version).IsUnique();
    }
}

public sealed class PrivateUserBlockConfiguration : IEntityTypeConfiguration<PrivateUserBlock>
{
    public void Configure(EntityTypeBuilder<PrivateUserBlock> builder)
    {
        builder.ToTable("PrivateUserBlocks");
        builder.HasVersionedKey();
        builder.HasIndex(nameof(PrivateUserBlock.UserId), nameof(PrivateUserBlock.BlockedUserId), VersionedColumns.Version).IsUnique();
    }
}
