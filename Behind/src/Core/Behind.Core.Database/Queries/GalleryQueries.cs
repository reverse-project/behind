using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class GalleryQueries(AbstractCoreContext context) : IGalleryQueries
{
    public async Task<PagedResult<IGalleryQueries.GalleryListing>> List(
        Flipper flipper,
        Option<string> search,
        CancellationToken cancellationToken = default)
    {
        var matching = context.LiveGalleries().Matching(search);
        var total = await matching.LongCountAsync(cancellationToken);

        var rows = await flipper.Apply(context.OrderByCreation(matching, newestFirst: true).ToRows())
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var lookups = await context.GalleryLookupsAsync(rows, cancellationToken);

        return new PagedResult<IGalleryQueries.GalleryListing>(
            total,
            flipper.Index,
            flipper.PageCount(total),
            flipper.Size,
            rows.Count > 0
                ? Some<IEnumerable<IGalleryQueries.GalleryListing>>(rows.Select(row => ToListing(row, lookups)).ToList())
                : None);
    }

    public async Task<PagedResult<IGalleryQueries.ManagedGalleryListing>> ListManaged(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);
        var matching = Latest(search, state).InBucket(gallery => gallery.Title, letter);
        var total = await matching.LongCountAsync(cancellationToken);

        var rows = await flipper.Apply(context.OrderByCreation(matching, newestFirst: true).ToRows())
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var keys = rows.Select(row => row.Key.ToString()).ToList();
        var lookups = await context.GalleryLookupsAsync(rows, cancellationToken);
        var active = await context.ActiveVersionsAsync(Gallery.EntityType, keys, cancellationToken);

        var listings = rows
            .Select(row => new IGalleryQueries.ManagedGalleryListing(
                row.Key,
                row.Version,
                active.TryGetValue(row.Key.ToString(), out var version) ? Some(version) : None,
                row.Title,
                lookups.AuthorOf(row),
                lookups.CreatedAtOf(row),
                lookups.UpdatedAtOf(row),
                lookups.ThumbnailOf(row),
                lookups.CountOf(row)) { AuthorId = row.AuthorId })
            .ToList();

        return new PagedResult<IGalleryQueries.ManagedGalleryListing>(
            total,
            flipper.Index,
            flipper.PageCount(total),
            flipper.Size,
            listings.Count > 0 ? Some<IEnumerable<IGalleryQueries.ManagedGalleryListing>>(listings) : None);
    }

    public async Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);

        return await Latest(search, state).LettersAsync(gallery => gallery.Title, cancellationToken);
    }

    public async Task<Option<IGalleryQueries.GalleryDetail>> Find(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.LiveGalleries()
            .OfKey(id)
            .ToRows()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return await ToDetail(rows, cancellationToken);
    }

    public async Task<Option<IGalleryQueries.GalleryHistory>> FindHistory(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.ActiveGalleries()
            .OfKey(id)
            .ToRows()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return None;
        }

        var versions = rows.Select(row => row.Version).ToList();

        var active = (await context.ActiveVersionsAsync(Gallery.EntityType, [id.ToString()], cancellationToken))
            .Select(pair => pair.Value)
            .Head();

        var created = await context.VersionCreationTimesAsync<Gallery>(id, versions, cancellationToken);
        var counts = await context.GalleryItemCountsAsync(id, versions, cancellationToken);

        return Some(new IGalleryQueries.GalleryHistory(
            id,
            active,
            rows.OrderByDescending(row => row.Version)
                .Select(row => new IGalleryQueries.GalleryVersionInfo(
                    row.Version,
                    row.Title,
                    counts.GetValueOrDefault(row.Version),
                    created.TryGetValue(row.Version, out var at) ? Some(at) : None,
                    active.Match(() => false, version => version == row.Version)))
                .ToList()));
    }

    public async Task<Option<IGalleryQueries.GalleryDetail>> FindVersion(
        VersionedKey<Guid> id,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.ActiveGalleries()
            .OfVersion(id)
            .ToRows()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return await ToDetail(rows, cancellationToken);
    }

    private Task<Option<IGalleryQueries.GalleryDetail>> ToDetail(
        IReadOnlyList<GalleryRow> rows,
        CancellationToken cancellationToken) =>
        rows.Head().Match(
            () => Task.FromResult<Option<IGalleryQueries.GalleryDetail>>(None),
            async row => Some(await ToDetail(row, cancellationToken)));

    private async Task<IGalleryQueries.GalleryDetail> ToDetail(GalleryRow row, CancellationToken cancellationToken)
    {
        var lookups = await context.GalleryLookupsAsync([row], cancellationToken);
        var items = await context.GalleryItemsAsync(row.Id, cancellationToken);

        return new IGalleryQueries.GalleryDetail(
            row.Id,
            row.Title,
            row.Description,
            lookups.AuthorOf(row),
            lookups.CreatedAtOf(row),
            lookups.UpdatedAtOf(row),
            lookups.ThumbnailOf(row),
            row.ThumbnailGenerated,
            items) { AuthorId = row.AuthorId };
    }

    private static IGalleryQueries.GalleryListing ToListing(GalleryRow row, GalleryLookups lookups) =>
        new(
            row.Id,
            row.Title,
            row.Description,
            lookups.AuthorOf(row),
            lookups.CreatedAtOf(row),
            lookups.UpdatedAtOf(row),
            lookups.ThumbnailOf(row),
            lookups.CountOf(row)) { AuthorId = row.AuthorId };

    private IDisposable? Scope(DeletionState state) =>
        state == DeletionState.Deleted ? context.SoftDelete.IncludeDeleted() : null;

    private IQueryable<Gallery> Latest(Option<string> search, DeletionState state) =>
        context.Galleries
            .InDeletionState(context, AbstractCoreContext.DeletionRecorded, state)
            .LatestOf(context.Galleries)
            .Matching(search);
}
