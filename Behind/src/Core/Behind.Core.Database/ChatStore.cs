using System.Data;
using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Behind.Core.Database;

public sealed class ChatStore(AbstractCoreContext context, TimeProvider timeProvider, ILogger<ChatStore> logger) : IChatStore
{
    private IQueryable<ChatMessage> Latest =>
        context.ChatMessages.LatestOf(context.ChatMessages);

    private IQueryable<Row> Rows =>
        Latest.Select(message => new Row
        {
            Id = EF.Property<Guid>(message, VersionedColumns.Key),
            Version = EF.Property<long>(message, VersionedColumns.Version),
            AuthorId = message.AuthorId,
            Text = message.Text,
            ReplyToId = message.ReplyToId,
            IsAnonymous = message.IsAnonymous,
            CreatedAt = context.AuditLogEntries
                .Where(audit => audit.EntityName == nameof(ChatMessage)
                    && audit.EntityKey == EF.Property<Guid>(message, VersionedColumns.Key).ToString().ToLower() + ":1"
                    && audit.Action == new AuditLogEntry.Created())
                .Select(audit => audit.Timestamp)
                .Min()
        });

    public async Task<IChatStore.Page?> List(Guid? before, int size, CancellationToken cancellationToken)
    {
        var rows = Rows;

        if (before is { } cursor)
        {
            var anchor = await Creation(cursor, cancellationToken);

            if (anchor is null)
            {
                return null;
            }

            rows = rows.Where(row => row.CreatedAt < anchor.Value
                || row.CreatedAt == anchor.Value && row.Id.CompareTo(cursor) < 0);
        }

        var found = await rows.OrderByDescending(row => row.CreatedAt).ThenByDescending(row => row.Id)
            .Take(size + 1).AsNoTracking().ToListAsync(cancellationToken);
        var page = found.Take(size).ToArray();

        return new IChatStore.Page(
            await Messages(page.Reverse().ToArray(), cancellationToken),
            found.Count > size ? page[^1].Id : null);
    }

    public async Task<IReadOnlyList<IChatStore.Message>> Find(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await Messages(await Rows.Where(row => ids.Contains(row.Id)).AsNoTracking().ToListAsync(cancellationToken),
            cancellationToken);

    public async Task<IChatStore.SaveResult> Add(
        Guid authorId, string text, Guid? replyToId, CancellationToken cancellationToken, bool isAnonymous = false)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        if (replyToId is { } parent && !await Latest.OfKey(parent).AnyAsync(cancellationToken))
        {
            return new(IChatStore.SaveStatus.InvalidReply);
        }

        var id = Guid.CreateVersion7();
        var result = await Save(new ChatMessage(new(id, 1), authorId, text, replyToId, isAnonymous), cancellationToken);

        if (result.Status == IChatStore.SaveStatus.Saved)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return result;
    }

    public async Task<IChatStore.SaveResult> Update(
        Guid id, long version, Guid authorId, string text, CancellationToken cancellationToken, bool? isAnonymous = null)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var found = await Latest.OfKey(id)
            .Select(message => new { Version = EF.Property<long>(message, VersionedColumns.Version), message.AuthorId, message.ReplyToId, message.IsAnonymous })
            .SingleOrDefaultAsync(cancellationToken);

        if (found is null)
        {
            return new(IChatStore.SaveStatus.NotFound);
        }

        if (found.AuthorId != authorId)
        {
            return new(IChatStore.SaveStatus.Forbidden);
        }

        var created = await Creation(id, cancellationToken);

        if (created is null || timeProvider.GetUtcNow() >= created.Value + ChatMessage.EditWindow)
        {
            return new(IChatStore.SaveStatus.Expired);
        }

        if (found.Version != version)
        {
            return new(IChatStore.SaveStatus.Conflict);
        }

        var result = await Save(new ChatMessage(new(id, version + 1), authorId, text, found.ReplyToId,
            isAnonymous ?? found.IsAnonymous), cancellationToken);

        if (result.Status == IChatStore.SaveStatus.Saved)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return result;
    }

    public async Task<bool> Delete(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var deleted = await context.MarkSoftDeletedAsync(context.ChatMessages.OfKey(id), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return deleted > 0;
    }

    private async Task<IChatStore.SaveResult> Save(ChatMessage message, CancellationToken cancellationToken)
    {
        await context.ChatMessages.AddAsync(message, cancellationToken);
        context.Entry(message).StampVersionedKey(message.Id.Key, message.Id.Version);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return new(IChatStore.SaveStatus.Saved, message.Id.Key);
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Chat message {MessageId} version {Version} could not be saved.", message.Id.Key, message.Id.Version);
            context.ChangeTracker.Clear();
            return new(IChatStore.SaveStatus.Conflict);
        }
    }

    private async Task<DateTimeOffset?> Creation(Guid id, CancellationToken cancellationToken)
    {
        var key = $"{id.ToString().ToLowerInvariant()}:1";
        return await context.AuditLogEntries
            .Where(audit => audit.EntityName == nameof(ChatMessage) && audit.EntityKey == key
                && audit.Action == new AuditLogEntry.Created())
            .Select(audit => (DateTimeOffset?)audit.Timestamp).MinAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<IChatStore.Message>> Messages(
        IReadOnlyCollection<Row> rows, CancellationToken cancellationToken)
    {
        var times = await context.AuditTimesAsync<ChatMessage>(
            rows.ToDictionary(row => row.Id, row => row.Version), cancellationToken);
        return rows.Select(row => new IChatStore.Message(row.Id, row.Version, row.AuthorId, row.Text, row.ReplyToId,
            row.CreatedAt, times[row.Id].UpdatedAt.ToNullable(), row.IsAnonymous)).ToArray();
    }

    private sealed record Row
    {
        public Guid Id { get; init; }
        public long Version { get; init; }
        public Guid AuthorId { get; init; }
        public required string Text { get; init; }
        public Guid? ReplyToId { get; init; }
        public bool IsAnonymous { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }
}
