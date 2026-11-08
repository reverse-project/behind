using System.Linq.Expressions;
using Behind.Common;
using Behind.Common.Database;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Sport.Database.Queries;

public abstract class ListingQueries<TEntity, TListing>(ISportContext context) : IListingQueries<TListing>
    where TEntity : class
{
    protected ISportContext Context => context;

    protected SportLookups Lookups { get; } = new(context);

    protected abstract IQueryable<TEntity> Set { get; }

    protected virtual Option<Expression<Func<TEntity, string>>> Label => None;

    protected static Option<Expression<Func<TEntity, string>>> LabelledBy(Expression<Func<TEntity, string>> label) =>
        Some(label);

    protected abstract IOrderedQueryable<TEntity> Order(IQueryable<TEntity> rows);

    protected abstract IQueryable<TEntity> Narrow(IQueryable<TEntity> rows, SportFilter filter, Option<string> term);

    protected abstract Task<IReadOnlyList<TListing>> Present(
        IReadOnlyList<Keyed<TEntity>> rows,
        DeletionState state,
        CancellationToken cancellationToken);

    public async Task<PagedResult<TListing>> List(ListRequest request, CancellationToken cancellationToken = default)
    {
        using var scope = Scope(request.State);
        var matching = InBucket(Matching(request), request.Letter);
        var total = await matching.LongCountAsync(cancellationToken);

        var rows = await request.Flipper
            .Apply(Order(matching).Keyed())
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return request.Flipper.ToPage(total, await Present(rows, request.State, cancellationToken));
    }

    public async Task<IReadOnlyList<string>> GetLetters(
        ListRequest request,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(request.State);

        return await Label.Match<Task<IReadOnlyList<string>>>(
            () => Task.FromResult<IReadOnlyList<string>>([]),
            label => Matching(request).LettersAsync(label, cancellationToken));
    }

    public async Task<Option<TListing>> Find(Guid id, CancellationToken cancellationToken = default)
    {
        var latest = (await Set.OfKey(id).ToKeyedAsync(cancellationToken)).Latest().ToList();

        return latest.Count == 0
            ? None
            : Some((await Present(latest, DeletionState.Active, cancellationToken))[0]);
    }

    private IQueryable<TEntity> Matching(ListRequest request) =>
        Narrow(
            context.InDeletionState(Set, request.State).LatestOf(Set),
            request.Filter,
            request.Search.Map(term => term.Trim().ToLowerInvariant()).Where(term => term.Length > 0));

    private IQueryable<TEntity> InBucket(IQueryable<TEntity> rows, Option<string> letter) =>
        Label.Match(() => rows, label => rows.InBucket(label, letter));

    private IDisposable? Scope(DeletionState state) =>
        state == DeletionState.Deleted ? context.SoftDelete.IncludeDeleted() : null;
}
