using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public sealed class PrivateMessageEmailStore(AbstractCoreContext context)
{
    public async Task Enqueue(Guid messageId, CancellationToken cancellationToken)
    {
        var message = await context.PrivateMessages.LatestOf(context.PrivateMessages).OfKey(messageId)
            .AsNoTracking().SingleAsync(cancellationToken);
        var conversation = await context.PrivateConversations.LatestOf(context.PrivateConversations)
            .OfKey(message.ConversationId).AsNoTracking().SingleAsync(cancellationToken);
        var recipients = await context.PrivateConversationRecipients.LatestOf(context.PrivateConversationRecipients)
            .AsNoTracking().Where(recipient => recipient.ConversationId == message.ConversationId)
            .Select(recipient => new PrivateMessagingRecipient(recipient.RecipientId, recipient.IsRole, recipient.IsHidden))
            .ToListAsync(cancellationToken);
        var roleIds = recipients.Where(recipient => recipient.IsRole).Select(recipient => recipient.Id).ToArray();
        var activeRoles = await context.Roles.Where(role => roleIds.Contains(EF.Property<Guid>(role, "Id")))
            .Select(role => EF.Property<Guid>(role, "Id")).ToArrayAsync(cancellationToken);
        var memberships = await context.Set<Dictionary<string, object>>("UserRoles")
            .Where(row => activeRoles.Contains(EF.Property<Guid>(row, "RoleId")))
            .Select(row => new { UserId = EF.Property<Guid>(row, "UserId"), RoleId = EF.Property<Guid>(row, "RoleId") })
            .ToListAsync(cancellationToken);
        var candidates = recipients.Where(recipient => !recipient.IsRole).Select(recipient => recipient.Id)
            .Concat(memberships.Select(member => member.UserId)).Append(conversation.SenderId)
            .Where(id => id != message.AuthorId && id != DomainConstants.SystemUserId && id != DomainConstants.GuestUserId)
            .Distinct().ToArray();
        var left = await context.PrivateMailboxes.LatestOf(context.PrivateMailboxes)
            .Where(mailbox => mailbox.ConversationId == message.ConversationId && mailbox.Left)
            .Select(mailbox => mailbox.UserId).ToArrayAsync(cancellationToken);
        var audience = candidates.Where(id => !left.Contains(id) && PrivateMessagingVisibility.CanReadMessage(
            new(message.ConversationId, conversation.SenderId, conversation.IsSystem, conversation.IsReport), recipients,
            new(messageId, message.ConversationId, message.AuthorId, message.PrivateRecipientId, message.IsInitial),
            new(id, memberships.Where(member => member.UserId == id).Select(member => member.RoleId).ToHashSet(), false)))
            .ToArray();
        var destinations = await (from user in context.Users
                                  join email in context.Emails on EF.Property<Guid>(user, "Id") equals EF.Property<Guid>(email, "UserId")
                                  where audience.Contains(EF.Property<Guid>(user, "Id")) && email.ConfirmedAt != null
                                  select new { UserId = EF.Property<Guid>(user, "Id"), email.Value })
            .AsNoTracking().ToListAsync(cancellationToken);
        foreach (var destination in destinations.DistinctBy(destination => (destination.UserId, destination.Value.ToLowerInvariant())))
        {
            Stage(new(new(Guid.NewGuid(), 1), message.ConversationId, messageId, destination.UserId, destination.Value, "pending", 0));
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PrivateMessageEmailDelivery>> Pending(CancellationToken cancellationToken)
    {
        if (await context.Set<PrivateMessageEmailDelivery>().LatestOf(context.Set<PrivateMessageEmailDelivery>())
                .AnyAsync(delivery => delivery.Attempts < 0
                    || (delivery.Status != "pending" && delivery.Status != "sending" && delivery.Status != "failed"
                        && delivery.Status != "sent" && delivery.Status != "cancelled"), cancellationToken))
        {
            throw new InvalidOperationException("Invalid private message email delivery state.");
        }
        var rows = await context.Set<PrivateMessageEmailDelivery>().LatestOf(context.Set<PrivateMessageEmailDelivery>()).AsNoTracking()
            .Where(delivery => (delivery.Status == "pending" || delivery.Status == "failed") && delivery.Attempts < 3)
            .OrderBy(delivery => EF.Property<Guid>(delivery, "Key")).Take(100).ToKeyedAsync(cancellationToken);
        return rows.Select(row => row.Entity with { Id = new(row.Key, row.Version) }).ToArray();
    }

    public async Task<PrivateMessageEmailDelivery?> Find(Guid id, CancellationToken cancellationToken)
    {
        var row = (await context.Set<PrivateMessageEmailDelivery>().LatestOf(context.Set<PrivateMessageEmailDelivery>())
            .AsNoTracking().OfKey(id).ToKeyedAsync(cancellationToken)).SingleOrDefault();
        return row is null ? null : row.Entity with { Id = new(row.Key, row.Version) };
    }

    public void Stage(PrivateMessageEmailDelivery delivery)
    {
        context.Add(delivery);
        context.Entry(delivery).StampVersionedKey(delivery.Id.Key, delivery.Id.Version);
    }
}
