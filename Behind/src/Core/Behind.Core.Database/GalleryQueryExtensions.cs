using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database;

internal sealed record GalleryRow(
    Guid Key,
    long Version,
    string Title,
    string Description,
    Guid AuthorId,
    Guid? ThumbnailImageId,
    bool ThumbnailGenerated)
{
    public VersionedKey<Guid> Id => new(Key, Version);
}

internal sealed record GalleryItemSummary(int Count, Option<Guid> FirstThumbnailId);

internal sealed record GalleryLookups(
    IReadOnlyDictionary<Guid, string> Authors,
    IReadOnlyDictionary<Guid, AuditTimes> Times,
    IReadOnlyDictionary<Guid, GalleryItemSummary> Items)
{
    public string AuthorOf(GalleryRow row) => Authors.GetValueOrDefault(row.AuthorId, string.Empty);

    public Option<DateTimeOffset> CreatedAtOf(GalleryRow row) =>
        Times.TryGetValue(row.Key, out var times) ? times.CreatedAt : FunctionalExtensions.None;

    public Option<DateTimeOffset> UpdatedAtOf(GalleryRow row) =>
        Times.TryGetValue(row.Key, out var times) ? times.UpdatedAt : FunctionalExtensions.None;

    public int CountOf(GalleryRow row) => Items.TryGetValue(row.Key, out var summary) ? summary.Count : 0;

    public Option<Guid> ThumbnailOf(GalleryRow row) =>
        row.ThumbnailImageId.ToOption().Match(
            () => Items.TryGetValue(row.Key, out var summary) ? summary.FirstThumbnailId : FunctionalExtensions.None,
            thumbnail => Some(thumbnail));
}

internal static class GalleryQueryExtensions
{
    extension(IQueryable<Gallery> galleries)
    {
        public IQueryable<GalleryRow> ToRows() =>
            galleries.Select(item => new GalleryRow(
                EF.Property<Guid>(item, VersionedColumns.Key),
                EF.Property<long>(item, VersionedColumns.Version),
                item.Title,
                item.Description,
                EF.Property<Guid>(item, GalleryConfiguration.AuthorIdProperty),
                EF.Property<Guid?>(item, GalleryConfiguration.ThumbnailImageIdProperty),
                EF.Property<bool>(item, GalleryConfiguration.ThumbnailGeneratedProperty)));

        public IQueryable<Gallery> Matching(Option<string> search) =>
            search
                .Map(term => term.Trim().ToLowerInvariant())
                .Where(term => term.Length > 0)
                .Match(
                    () => galleries,
                    term => galleries.Where(item =>
                        item.Title.ToLower().Contains(term) || item.Description.ToLower().Contains(term)));
    }

    extension(AbstractCoreContext context)
    {
        public IQueryable<Gallery> ActiveGalleries() =>
            context.Galleries.InDeletionState(context, AbstractCoreContext.DeletionRecorded, DeletionState.Active);

        public IQueryable<Gallery> LiveGalleries() =>
            context.ActiveGalleries().LiveIn(context.ActiveVersions, Gallery.EntityType);

        public async Task<GalleryLookups> GalleryLookupsAsync(
            IReadOnlyCollection<GalleryRow> rows,
            CancellationToken cancellationToken)
        {
            var shown = rows.ToDictionary(row => row.Key, row => row.Version);

            return new GalleryLookups(
                await context.DisplayNamesAsync(rows.Select(row => row.AuthorId).Distinct().ToList(), cancellationToken),
                await context.AuditTimesAsync<Gallery>(shown, cancellationToken),
                await context.GalleryItemSummariesAsync(shown, cancellationToken));
        }

        public async Task<IReadOnlyList<IGalleryQueries.GalleryItemInfo>> GalleryItemsAsync(
            VersionedKey<Guid> gallery,
            CancellationToken cancellationToken)
        {
            var rows = await context.GalleryItems
                .Where(item => EF.Property<Guid>(item, GalleryItemConfiguration.GalleryIdProperty) == gallery.Key
                               && EF.Property<long>(item, VersionedColumns.Version) == gallery.Version)
                .OrderBy(item => EF.Property<int>(item, GalleryItemConfiguration.PositionProperty))
                .Select(item => new IGalleryQueries.GalleryItemInfo(
                    EF.Property<Guid>(item, VersionedColumns.Key),
                    item.Description,
                    EF.Property<Guid>(item, GalleryItemConfiguration.ImageIdProperty),
                    EF.Property<Guid>(item, GalleryItemConfiguration.ThumbnailIdProperty),
                    EF.Property<int>(item, GalleryItemConfiguration.PositionProperty)))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return rows;
        }

        public async Task<IReadOnlyDictionary<long, int>> GalleryItemCountsAsync(
            Guid gallery,
            IReadOnlyCollection<long> versions,
            CancellationToken cancellationToken)
        {
            var counts = await context.GalleryItems
                .Where(item => EF.Property<Guid>(item, GalleryItemConfiguration.GalleryIdProperty) == gallery
                               && versions.Contains(EF.Property<long>(item, VersionedColumns.Version)))
                .GroupBy(item => EF.Property<long>(item, VersionedColumns.Version))
                .Select(group => new { Version = group.Key, Count = group.Count() })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return counts.ToDictionary(item => item.Version, item => item.Count);
        }

        private async Task<IReadOnlyDictionary<Guid, GalleryItemSummary>> GalleryItemSummariesAsync(
            IReadOnlyDictionary<Guid, long> shown,
            CancellationToken cancellationToken)
        {
            var keys = shown.Keys.ToList();
            var versions = shown.Values.Distinct().ToList();

            var items = await context.GalleryItems
                .Where(item => keys.Contains(EF.Property<Guid>(item, GalleryItemConfiguration.GalleryIdProperty))
                               && versions.Contains(EF.Property<long>(item, VersionedColumns.Version)))
                .Select(item => new
                {
                    GalleryId = EF.Property<Guid>(item, GalleryItemConfiguration.GalleryIdProperty),
                    Version = EF.Property<long>(item, VersionedColumns.Version),
                    Position = EF.Property<int>(item, GalleryItemConfiguration.PositionProperty),
                    ThumbnailId = EF.Property<Guid>(item, GalleryItemConfiguration.ThumbnailIdProperty)
                })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return items
                .Where(item => shown[item.GalleryId] == item.Version)
                .GroupBy(item => item.GalleryId)
                .ToDictionary(
                    group => group.Key,
                    group => new GalleryItemSummary(
                        group.Count(),
                        Some(group.OrderBy(item => item.Position).First().ThumbnailId)));
        }
    }
}
