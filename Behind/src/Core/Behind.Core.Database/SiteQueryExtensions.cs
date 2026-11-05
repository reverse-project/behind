using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database;

internal sealed record SiteRow(Site Entity, Guid SiteId, long Version, DateTimeOffset? PublishedAt)
{
    public Site ToSite() =>
        Entity.WithId(new VersionedKey<Guid>(SiteId, Version)).WithPublishedAt(PublishedAt.ToOption());
}

internal sealed record SectionRow(SiteSection Entity, Guid Key, Guid? ImageId, int Position)
{
    public ISiteQueries.SiteSectionInfo ToInfo() =>
        new(
            Key,
            Entity.Theme,
            Entity.Title,
            Entity.Content,
            ImageId.ToOption(),
            Entity.ImageAlt,
            Entity.ImagePosition,
            Position);
}

internal static class SiteQueryExtensions
{
    extension(IQueryable<Site> sites)
    {
        public IQueryable<SiteRow> ToRows() =>
            sites.Select(site => new SiteRow(
                site,
                EF.Property<Guid>(site, SiteConfiguration.SiteIdProperty),
                EF.Property<long>(site, SiteConfiguration.VersionProperty),
                EF.Property<DateTimeOffset?>(site, SiteConfiguration.PublishedAtProperty)));

        public IQueryable<Site> OfSite(Guid siteId) =>
            sites.Where(site => EF.Property<Guid>(site, SiteConfiguration.SiteIdProperty) == siteId);

        public IQueryable<Site> OfVersion(VersionedKey<Guid> id) =>
            sites.OfSite(id.Key).Where(site => EF.Property<long>(site, SiteConfiguration.VersionProperty) == id.Version);
    }

    extension(AbstractVersioningContext versions)
    {
        public async Task<IReadOnlyDictionary<Guid, long>> ActiveSiteVersionsAsync(
            IReadOnlyCollection<Guid> siteIds,
            CancellationToken cancellationToken)
        {
            var active = await versions.ActiveVersionsAsync(
                Site.EntityType,
                siteIds.Select(id => id.ToKey()).ToList(),
                cancellationToken);

            return active
                .Select(pair => (Id: Guid.TryParse(pair.Key, out var id) ? id : (Guid?)null, Version: pair.Value))
                .Where(pair => pair.Id.HasValue)
                .ToDictionary(pair => pair.Id!.Value, pair => pair.Version);
        }
    }

    extension(AbstractCoreContext context)
    {
        public async Task<IReadOnlyDictionary<Guid, long>> LatestSiteVersionsAsync(
            IReadOnlyCollection<Guid> siteIds,
            CancellationToken cancellationToken)
        {
            var latest = await context.Sites
                .Where(site => siteIds.Contains(EF.Property<Guid>(site, SiteConfiguration.SiteIdProperty)))
                .GroupBy(site => EF.Property<Guid>(site, SiteConfiguration.SiteIdProperty))
                .Select(group => new { SiteId = group.Key, Version = group.Max(site => EF.Property<long>(site, SiteConfiguration.VersionProperty)) })
                .ToListAsync(cancellationToken);

            return latest.ToDictionary(item => item.SiteId, item => item.Version);
        }

        public async Task<IReadOnlyList<ISiteQueries.SiteSectionInfo>> SiteSectionsAsync(
            VersionedKey<Guid> site,
            CancellationToken cancellationToken)
        {
            var rows = await context.SiteSections
                .Where(section => EF.Property<Guid>(section, SiteSectionConfiguration.SiteIdProperty) == site.Key
                                  && EF.Property<long>(section, VersionedColumns.Version) == site.Version)
                .OrderBy(section => EF.Property<int>(section, SiteSectionConfiguration.PositionProperty))
                .Select(section => new SectionRow(
                    section,
                    EF.Property<Guid>(section, VersionedColumns.Key),
                    EF.Property<Guid?>(section, SiteSectionConfiguration.ImageIdProperty),
                    EF.Property<int>(section, SiteSectionConfiguration.PositionProperty)))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return rows.Select(row => row.ToInfo()).ToList();
        }

        public async Task<bool> IsSiteSlugTakenAsync(
            AbstractVersioningContext versions,
            Guid siteId,
            string slug,
            CancellationToken cancellationToken)
        {
            var candidates = await context.Sites
                .Where(site => site.Slug == slug && EF.Property<Guid>(site, SiteConfiguration.SiteIdProperty) != siteId)
                .ToRows()
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            if (candidates.Count == 0)
            {
                return false;
            }

            var ids = candidates.Select(candidate => candidate.SiteId).Distinct().ToList();
            var latest = await context.LatestSiteVersionsAsync(ids, cancellationToken);
            var active = await versions.ActiveSiteVersionsAsync(ids, cancellationToken);

            return candidates.Any(candidate =>
                latest.GetValueOrDefault(candidate.SiteId) == candidate.Version
                || active.GetValueOrDefault(candidate.SiteId) == candidate.Version);
        }
    }

    extension(Guid siteId)
    {
        public string ToKey() => siteId.ToString();
    }
}
