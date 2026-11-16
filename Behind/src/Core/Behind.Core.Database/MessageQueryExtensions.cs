using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

internal sealed record MessageRow(
    Guid Key,
    long Version,
    string Name,
    string Title,
    string Content,
    MessageLevel Level,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt);

internal static class MessageQueryExtensions
{
    extension(IQueryable<Message> messages)
    {
        public IQueryable<Message> Ordered() =>
            messages
                .OrderBy(message => message.Name)
                .ThenBy(message => EF.Property<Guid>(message, VersionedColumns.Key));

        public IQueryable<Message> LiveAt(DateTimeOffset now) =>
            messages.Where(message => (message.StartsAt == null || message.StartsAt <= now)
                                      && (message.EndsAt == null || message.EndsAt > now));

        public IQueryable<MessageRow> ToRows() =>
            messages.Select(message => new MessageRow(
                EF.Property<Guid>(message, VersionedColumns.Key),
                EF.Property<long>(message, VersionedColumns.Version),
                message.Name,
                message.Title,
                message.Content,
                message.Level,
                message.StartsAt,
                message.EndsAt));
    }

    extension(AbstractCoreContext context)
    {
        public IQueryable<Message> LatestMessagesIn(DeletionState state) =>
            context.Messages
                .InDeletionState(context, AbstractCoreContext.DeletionRecorded, state)
                .LatestOf(context.Messages);
    }
}
