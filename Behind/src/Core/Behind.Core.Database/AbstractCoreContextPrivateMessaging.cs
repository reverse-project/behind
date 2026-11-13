using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public partial class AbstractCoreContext
{
    public DbSet<PrivateConversation> PrivateConversations => Set<PrivateConversation>();
    public DbSet<PrivateConversationRecipient> PrivateConversationRecipients => Set<PrivateConversationRecipient>();
    public DbSet<PrivateMessage> PrivateMessages => Set<PrivateMessage>();
    public DbSet<PrivateMailbox> PrivateMailboxes => Set<PrivateMailbox>();
    public DbSet<PrivateMessageRead> PrivateMessageReads => Set<PrivateMessageRead>();
    public DbSet<PrivateUserBlock> PrivateUserBlocks => Set<PrivateUserBlock>();
}
