using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class SiteQueries(AbstractCoreContext context, AbstractVersioningContext versions) : ISiteQueries
{
    public async Task<PagedResult<ISiteQueries.SiteListing>> List(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);
        var matching = Matching(search, state).InBucket(site => site.Title, letter);
        var total = await matching.LongCountAsync(cancellationToken);

        var latest = await flipper.Apply(
                matching
                    .OrderBy(site => site.Title.ToLower())
                    .ThenBy(site => EF.Property<Guid>(site, SiteConfiguration.SiteIdProperty))
                    .Select(site => new LatestRow(
                        EF.Property<Guid>(site, SiteConfiguration.SiteIdProperty),
                        EF.Property<long>(site, SiteConfiguration.VersionProperty),
                        site.Title,
                        site.Slug,
                        site.CreatedAt)))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var siteIds = latest.Select(row => row.SiteId).ToList();
        var active = await versions.ActiveSiteVersionsAsync(siteIds, cancellationToken);
        var activeSlugs = await ActiveSlugsAsync(siteIds, active, cancellationToken);

        var listings = latest
            .Select(row => new ISiteQueries.SiteListing(
                row.SiteId,
                row.Title,
                row.Slug,
                row.Version,
                active.TryGetValue(row.SiteId, out var version) ? Some(version) : None,
                activeSlugs.TryGetValue(row.SiteId, out var slug) ? Some(slug) : None,
                row.CreatedAt))
            .ToList();

        return new PagedResult<ISiteQueries.SiteListing>(
            total,
            flipper.Index,
            flipper.PageCount(total),
            flipper.Size,
            listings.Count > 0 ? Some<IEnumerable<ISiteQueries.SiteListing>>(listings) : None);
    }

    public async Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);

        return await Matching(search, state).LettersAsync(site => site.Title, cancellationToken);
    }

    private IDisposable? Scope(DeletionState state) =>
        state == DeletionState.Deleted ? context.SoftDelete.IncludeDeleted() : null;

    private IQueryable<Site> Matching(Option<string> search, DeletionState state)
    {
        var latest = context.Sites
            .InDeletionState(context, AbstractCoreContext.DeletionRecorded, state)
            .Where(site => EF.Property<long>(site, SiteConfiguration.VersionProperty) == context.Sites
                .Where(other => EF.Property<Guid>(other, SiteConfiguration.SiteIdProperty)
                                == EF.Property<Guid>(site, SiteConfiguration.SiteIdProperty))
                .Max(other => EF.Property<long>(other, SiteConfiguration.VersionProperty)));

        return search
            .Map(term => term.Trim().ToLowerInvariant())
            .Where(term => term.Length > 0)
            .Match(
                () => latest,
                term => latest.Where(site => site.Title.ToLower().Contains(term) || site.Slug.Contains(term)));
    }

    public async Task<Option<ISiteQueries.SiteHistory>> FindHistory(
        Guid siteId,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.Sites
            .OfSite(siteId)
            .Select(site => new VersionRow(
                EF.Property<long>(site, SiteConfiguration.VersionProperty),
                site.Title,
                site.Slug,
                EF.Property<DateTimeOffset?>(site, SiteConfiguration.PublishedAtProperty),
                site.CreatedAt))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return None;
        }

        var active = (await versions.ActiveSiteVersionsAsync([siteId], cancellationToken))
            .Select(pair => pair.Value)
            .Head();

        return Some(new ISiteQueries.SiteHistory(
            siteId,
            active,
            rows.OrderByDescending(row => row.Version)
                .Select(row => new ISiteQueries.SiteVersionInfo(
                    row.Version,
                    row.Title,
                    row.Slug,
                    row.PublishedAt.ToOption(),
                    row.CreatedAt,
                    active.Match(() => false, version => version == row.Version)))
                .ToList()));
    }

    public async Task<Option<Site>> FindVersion(VersionedKey<Guid> id, CancellationToken cancellationToken = default)
    {
        var rows = await context.Sites
            .OfVersion(id)
            .ToRows()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows.Head().Map(row => row.ToSite());
    }

    public Task<IReadOnlyList<ISiteQueries.SiteSectionInfo>> FindSections(
        VersionedKey<Guid> id,
        CancellationToken cancellationToken = default) =>
        context.SiteSectionsAsync(id, cancellationToken);

    public async Task<Option<Site>> FindActiveBySlug(string slug, CancellationToken cancellationToken = default)
    {
        var candidates = await context.Sites
            .Where(site => site.Slug == slug)
            .ToRows()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var active = await versions.ActiveSiteVersionsAsync(
            candidates.Select(row => row.SiteId).Distinct().ToList(),
            cancellationToken);

        return candidates
            .Where(row => active.GetValueOrDefault(row.SiteId) == row.Version)
            .Head()
            .Map(row => row.ToSite());
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ActiveSlugsAsync(
        IReadOnlyCollection<Guid> siteIds,
        IReadOnlyDictionary<Guid, long> active,
        CancellationToken cancellationToken)
    {
        var activeVersions = active.Values.ToList();

        var rows = await context.Sites
            .Where(site => siteIds.Contains(EF.Property<Guid>(site, SiteConfiguration.SiteIdProperty))
                           && activeVersions.Contains(EF.Property<long>(site, SiteConfiguration.VersionProperty)))
            .Select(site => new SlugRow(
                EF.Property<Guid>(site, SiteConfiguration.SiteIdProperty),
                EF.Property<long>(site, SiteConfiguration.VersionProperty),
                site.Slug))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows
            .Where(row => active.GetValueOrDefault(row.SiteId) == row.Version)
            .ToDictionary(row => row.SiteId, row => row.Slug);
    }

    private sealed record SlugRow(Guid SiteId, long Version, string Slug);

    private sealed record LatestRow(Guid SiteId, long Version, string Title, string Slug, DateTimeOffset CreatedAt);

    private sealed record VersionRow(
        long Version,
        string Title,
        string Slug,
        DateTimeOffset? PublishedAt,
        DateTimeOffset CreatedAt);
}
