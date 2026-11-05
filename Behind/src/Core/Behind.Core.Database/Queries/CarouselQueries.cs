using Behind.Common;
using Behind.Common.Database;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class CarouselQueries(AbstractCoreContext context) : ICarouselQueries
{
    public async Task<Option<ICarouselQueries.CarouselDetail>> FindLive(
        Guid siteId,
        CancellationToken cancellationToken = default)
    {
        if (!await context.LiveSitesOf(siteId).AnyAsync(cancellationToken))
        {
            return None;
        }

        var rows = await context.LiveCarousels()
            .OfKey(siteId)
            .ToRows()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return await ToDetail(rows, cancellationToken);
    }

    public async Task<Option<ICarouselQueries.CarouselDetail>> FindManaged(
        Guid siteId,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = state == DeletionState.Deleted ? context.SoftDelete.IncludeDeleted() : null;

        var rows = await context.Carousels
            .InDeletionState(context, AbstractCoreContext.DeletionRecorded, state)
            .OfKey(siteId)
            .OrderByDescending(carousel => EF.Property<long>(carousel, VersionedColumns.Version))
            .ToRows()
            .Take(1)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return await ToDetail(rows, cancellationToken);
    }

    private Task<Option<ICarouselQueries.CarouselDetail>> ToDetail(
        IReadOnlyList<CarouselRow> rows,
        CancellationToken cancellationToken) =>
        rows.Head().Match(
            () => Task.FromResult<Option<ICarouselQueries.CarouselDetail>>(None),
            async row => Some(new ICarouselQueries.CarouselDetail(
                row.Id,
                row.IntervalSeconds,
                await context.CarouselSlidesAsync(row.Id, cancellationToken))));
}
