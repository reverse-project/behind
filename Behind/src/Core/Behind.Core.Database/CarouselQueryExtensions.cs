using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

internal sealed record CarouselRow(Guid Key, long Version, int IntervalSeconds)
{
    public VersionedKey<Guid> Id => new(Key, Version);
}

internal static class CarouselQueryExtensions
{
    extension(IQueryable<Carousel> carousels)
    {
        public IQueryable<CarouselRow> ToRows() =>
            carousels.Select(item => new CarouselRow(
                EF.Property<Guid>(item, VersionedColumns.Key),
                EF.Property<long>(item, VersionedColumns.Version),
                item.IntervalSeconds));
    }

    extension(AbstractCoreContext context)
    {
        public IQueryable<Carousel> ActiveCarousels() =>
            context.Carousels.InDeletionState(context, AbstractCoreContext.DeletionRecorded, DeletionState.Active);

        public IQueryable<Carousel> LiveCarousels() =>
            context.ActiveCarousels().LiveIn(context.ActiveVersions, Carousel.EntityType);

        public IQueryable<Site> LiveSitesOf(Guid siteId)
        {
            var key = siteId.ToKey();

            return context.Sites
                .OfSite(siteId)
                .Where(site => context.ActiveVersions.Any(active =>
                    active.EntityType == Site.EntityType
                    && active.EntityKey == key
                    && active.Version == EF.Property<long>(site, SiteConfiguration.VersionProperty)));
        }

        public async Task<long> LatestCarouselVersionAsync(Guid siteId, CancellationToken cancellationToken)
        {
            using (context.SoftDelete.IncludeDeleted())
            {
                return await context.Carousels.LatestVersionAsync(siteId, cancellationToken);
            }
        }

        public async Task<IReadOnlyList<ICarouselQueries.CarouselSlideInfo>> CarouselSlidesAsync(
            VersionedKey<Guid> carousel,
            CancellationToken cancellationToken) =>
            await context.CarouselSlides
                .Where(slide => EF.Property<Guid>(slide, CarouselSlideConfiguration.CarouselIdProperty) == carousel.Key
                                && EF.Property<long>(slide, VersionedColumns.Version) == carousel.Version)
                .OrderBy(slide => EF.Property<int>(slide, CarouselSlideConfiguration.PositionProperty))
                .Select(slide => new ICarouselQueries.CarouselSlideInfo(
                    EF.Property<Guid>(slide, VersionedColumns.Key),
                    EF.Property<Guid>(slide, CarouselSlideConfiguration.ImageIdProperty),
                    slide.Caption,
                    slide.LinkUrl,
                    EF.Property<int>(slide, CarouselSlideConfiguration.PositionProperty)))
                .AsNoTracking()
                .ToListAsync(cancellationToken);
    }
}
