using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Commands;

public sealed class SiteCommands(
    AbstractCoreContext context,
    AbstractVersioningContext versions,
    TimeProvider timeProvider) : ISiteCommands
{
    public Task<SiteSaveResult> Create(
        string title,
        string slug,
        string content,
        bool publish,
        CancellationToken cancellationToken = default) =>
        AddVersion(Guid.CreateVersion7(), 0, title, slug, content, publish, cancellationToken);

    public async Task<SiteSaveResult> SaveVersion(
        Guid siteId,
        string title,
        string slug,
        string content,
        bool publish,
        CancellationToken cancellationToken = default)
    {
        var latest = (await context.LatestSiteVersionsAsync([siteId], cancellationToken)).Values.Head();

        return await latest.Match(
            () => Task.FromResult<SiteSaveResult>(new SiteSaveResult.NotFound()),
            version => AddVersion(siteId, version, title, slug, content, publish, cancellationToken));
    }

    public async Task<SiteSaveResult> Activate(VersionedKey<Guid> id, CancellationToken cancellationToken = default)
    {
        var site = await context.Sites.FindAsync([id.Key, id.Version], cancellationToken);

        if (site is null)
        {
            return new SiteSaveResult.NotFound();
        }

        if (await context.IsSiteSlugTakenAsync(versions, id.Key, site.Slug, cancellationToken))
        {
            return new SiteSaveResult.SlugTaken();
        }

        var entry = context.Entry(site);

        if (entry.Property<DateTimeOffset?>(SiteConfiguration.PublishedAtProperty).CurrentValue is null)
        {
            entry.Property<DateTimeOffset?>(SiteConfiguration.PublishedAtProperty).CurrentValue =
                timeProvider.GetUtcNow();
        }

        return await Save(id, cancellationToken, () => SetActive(id.Key, id.Version, cancellationToken));
    }

    private async Task<SiteSaveResult> AddVersion(
        Guid siteId,
        long previousVersion,
        string title,
        string slug,
        string content,
        bool publish,
        CancellationToken cancellationToken)
    {
        if (await context.IsSiteSlugTakenAsync(versions, siteId, slug, cancellationToken))
        {
            return new SiteSaveResult.SlugTaken();
        }

        var now = timeProvider.GetUtcNow();
        var version = previousVersion + 1;
        var site = new Site(title, slug, content, now);

        await context.Sites.AddAsync(site, cancellationToken);

        var entry = context.Entry(site);
        entry.Property<Guid>(SiteConfiguration.SiteIdProperty).CurrentValue = siteId;
        entry.Property<long>(SiteConfiguration.VersionProperty).CurrentValue = version;
        entry.Property<DateTimeOffset?>(SiteConfiguration.PublishedAtProperty).CurrentValue = publish ? now : null;

        return await Save(
            new VersionedKey<Guid>(siteId, version),
            cancellationToken,
            publish ? () => SetActive(siteId, version, cancellationToken) : null);
    }

    private async Task SetActive(Guid siteId, long version, CancellationToken cancellationToken)
    {
        await versions.SetActiveVersionAsync(Site.EntityType, siteId.ToKey(), version, cancellationToken);
        await versions.SaveChangesAsync(cancellationToken);
    }

    private async Task<SiteSaveResult> Save(
        VersionedKey<Guid> id,
        CancellationToken cancellationToken,
        Func<Task>? afterSave = null)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();

            return new SiteSaveResult.VersionConflict();
        }

        if (afterSave is not null)
        {
            await afterSave();
        }

        return new SiteSaveResult.Saved(id);
    }
}
