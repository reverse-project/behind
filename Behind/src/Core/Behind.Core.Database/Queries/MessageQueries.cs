using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class MessageQueries(AbstractCoreContext context) : IMessageQueries
{
    public async Task<PagedResult<IMessageQueries.MessageListing>> List(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);
        var matching = Matching(search, state).InBucket(message => message.Name, letter);
        var total = await matching.LongCountAsync(cancellationToken);

        var rows = await flipper.Apply(matching.Ordered().ToRows())
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new PagedResult<IMessageQueries.MessageListing>(
            total,
            flipper.Index,
            flipper.PageCount(total),
            flipper.Size,
            rows.Count > 0
                ? Some<IEnumerable<IMessageQueries.MessageListing>>(rows.Select(ToListing).ToList())
                : None);
    }

    public async Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);

        return await Matching(search, state).LettersAsync(message => message.Name, cancellationToken);
    }

    public async Task<Option<IMessageQueries.MessageDetails>> Find(
        Guid key,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);

        return await First(context.LatestMessagesIn(state).OfKey(key), cancellationToken);
    }

    public Task<Option<IMessageQueries.MessageDetails>> FindLive(
        Guid key,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        First(context.LatestMessagesIn(DeletionState.Active).OfKey(key).LiveAt(now), cancellationToken);

    private static async Task<Option<IMessageQueries.MessageDetails>> First(
        IQueryable<Message> messages,
        CancellationToken cancellationToken) =>
        (await messages.ToRows().Take(1).AsNoTracking().ToListAsync(cancellationToken))
        .Select(ToDetails)
        .Head();

    private static IMessageQueries.MessageListing ToListing(MessageRow row) =>
        new(new VersionedKey<Guid>(row.Key, row.Version), row.Name, row.Title, row.Level, row.StartsAt, row.EndsAt);

    private static IMessageQueries.MessageDetails ToDetails(MessageRow row) =>
        new(
            new VersionedKey<Guid>(row.Key, row.Version),
            row.Name,
            row.Title,
            row.Content,
            row.Level,
            row.StartsAt,
            row.EndsAt);

    private IDisposable? Scope(DeletionState state) =>
        state == DeletionState.Deleted ? context.SoftDelete.IncludeDeleted() : null;

    private IQueryable<Message> Matching(Option<string> search, DeletionState state)
    {
        var latest = context.LatestMessagesIn(state);

        return search
            .Map(term => term.Trim().ToLowerInvariant())
            .Where(term => term.Length > 0)
            .Match(
                () => latest,
                term => latest.Where(message =>
                    message.Name.ToLower().Contains(term) || message.Title.ToLower().Contains(term)));
    }
}
