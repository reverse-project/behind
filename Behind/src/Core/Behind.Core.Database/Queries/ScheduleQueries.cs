using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class ScheduleQueries(AbstractCoreContext context) : IScheduleQueries
{
    public async Task<PagedResult<IScheduleQueries.ScheduleListing>> List(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);
        var matching = Matching(search, state).InBucket(schedule => schedule.Name, letter);
        var total = await matching.LongCountAsync(cancellationToken);

        var rows = await flipper.Apply(matching.Ordered().ToRows())
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new PagedResult<IScheduleQueries.ScheduleListing>(
            total,
            flipper.Index,
            flipper.PageCount(total),
            flipper.Size,
            rows.Count > 0
                ? Some<IEnumerable<IScheduleQueries.ScheduleListing>>(rows.Select(ToListing).ToList())
                : None);
    }

    public async Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);

        return await Matching(search, state).LettersAsync(schedule => schedule.Name, cancellationToken);
    }

    public async Task<Option<IScheduleQueries.ScheduleDetails>> Find(
        Guid key,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);

        return (await context.LatestSchedulesIn(state).OfKey(key).ToRows().Take(1)
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .Select(ToDetails)
            .Head();
    }

    public async Task<IReadOnlyList<IScheduleQueries.ScheduleDetails>> Enabled(
        CancellationToken cancellationToken = default) =>
        (await context.LatestSchedulesIn(DeletionState.Active)
            .Where(schedule => schedule.Enabled)
            .Ordered()
            .ToRows()
            .AsNoTracking()
            .ToListAsync(cancellationToken))
        .Select(ToDetails)
        .ToList();

    private static IScheduleQueries.ScheduleListing ToListing(ScheduleRow row) =>
        new(new VersionedKey<Guid>(row.Key, row.Version), row.Name, row.Cron, row.Action, row.Enabled, row.Notify);

    private static IScheduleQueries.ScheduleDetails ToDetails(ScheduleRow row) =>
        new(
            new VersionedKey<Guid>(row.Key, row.Version),
            row.Name,
            row.Cron,
            row.Action,
            row.Parameters,
            row.Enabled,
            row.Notify,
            row.Participants);

    private IDisposable? Scope(DeletionState state) =>
        state == DeletionState.Deleted ? context.SoftDelete.IncludeDeleted() : null;

    private IQueryable<Schedule> Matching(Option<string> search, DeletionState state)
    {
        var latest = context.LatestSchedulesIn(state);

        return search
            .Map(term => term.Trim().ToLowerInvariant())
            .Where(term => term.Length > 0)
            .Match(
                () => latest,
                term => latest.Where(schedule =>
                    schedule.Name.ToLower().Contains(term) || schedule.Action.ToLower().Contains(term)));
    }
}
