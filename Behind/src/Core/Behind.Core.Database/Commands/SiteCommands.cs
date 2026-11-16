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
    TimeProvider timeProvider,
    IVfsCommands files,
    ICarouselCommands carousels) : ISiteCommands
{
    public Task<SiteSaveResult> Create(
        string title,
        string slug,
        string content,
        bool publish,
        bool hideTitle = false,
        IReadOnlyList<ISiteCommands.SiteSectionDraft>? sections = null,
        CancellationToken cancellationToken = default) =>
        AddVersion(Guid.CreateVersion7(), 0, title, slug, content, publish, hideTitle, sections ?? [], cancellationToken);

    public async Task<SiteSaveResult> SaveVersion(
        Guid siteId,
        string title,
        string slug,
        string content,
        bool publish,
        bool hideTitle = false,
        IReadOnlyList<ISiteCommands.SiteSectionDraft>? sections = null,
        CancellationToken cancellationToken = default)
    {
        var latest = (await context.LatestSiteVersionsAsync([siteId], cancellationToken)).Values.Head();

        return await latest.Match(
            () => Task.FromResult<SiteSaveResult>(new SiteSaveResult.NotFound()),
            version => AddVersion(siteId, version, title, slug, content, publish, hideTitle, sections ?? [], cancellationToken));
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

    public async Task<bool> Delete(Guid siteId, CancellationToken cancellationToken = default)
    {
        var rows = await context.Sites.OfSite(siteId).ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return false;
        }

        context.Sites.RemoveRange(rows);
        await context.SaveChangesAsync(cancellationToken);
        await carousels.Delete(siteId, cancellationToken);

        return true;
    }

    public async Task<SiteSaveResult> Restore(Guid siteId, CancellationToken cancellationToken = default)
    {
        var deleted = await DeletedRows(siteId, cancellationToken);

        if (deleted.Count == 0)
        {
            return new SiteSaveResult.NotFound();
        }

        var latest = deleted.Max(row => context.Entry(row).Property<long>(SiteConfiguration.VersionProperty).CurrentValue);
        var active = await versions.ActiveSiteVersionsAsync([siteId], cancellationToken);

        var contested = deleted
            .Where(row => IsLiveCandidate(row, latest, active.GetValueOrDefault(siteId)))
            .Select(row => row.Slug)
            .Distinct()
            .ToList();

        foreach (var slug in contested)
        {
            if (await context.IsSiteSlugTakenAsync(versions, siteId, slug, cancellationToken))
            {
                return new SiteSaveResult.SlugTaken();
            }
        }

        deleted.ForEach(row => context.Restores.Mark(row));

        var result = await Save(new VersionedKey<Guid>(siteId, latest), cancellationToken);

        if (result is SiteSaveResult.Saved)
        {
            await carousels.Restore(siteId, cancellationToken);
        }

        return result;
    }

    public async Task<HardDeleteResult> HardDelete(Guid siteId, CancellationToken cancellationToken = default)
    {
        var deleted = await DeletedRows(siteId, cancellationToken);

        if (deleted.Count == 0)
        {
            return HardDeleteResult.NotFound;
        }

        try
        {
            deleted.ForEach(context.HardDelete);
            context.SiteSections.RemoveRange(await SectionsOf(siteId, deleted, cancellationToken));
            await context.SaveChangesAsync(cancellationToken);

            await versions.ClearActiveVersionAsync(Site.EntityType, siteId.ToKey(), cancellationToken);
            await versions.SaveChangesAsync(cancellationToken);
            await carousels.HardDelete(siteId, cancellationToken);

            return HardDeleteResult.Deleted;
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();
            return HardDeleteResult.Rejected;
        }
    }

    private async Task<List<SiteSection>> SectionsOf(
        Guid siteId,
        IReadOnlyCollection<Site> rows,
        CancellationToken cancellationToken)
    {
        var rowVersions = rows
            .Select(row => context.Entry(row).Property<long>(SiteConfiguration.VersionProperty).CurrentValue)
            .ToList();

        return await context.SiteSections
            .Where(section => EF.Property<Guid>(section, SiteSectionConfiguration.SiteIdProperty) == siteId
                              && rowVersions.Contains(EF.Property<long>(section, VersionedColumns.Version)))
            .ToListAsync(cancellationToken);
    }

    private bool IsLiveCandidate(Site row, long latest, long active)
    {
        var version = context.Entry(row).Property<long>(SiteConfiguration.VersionProperty).CurrentValue;

        return version == latest || version == active;
    }

    private async Task<List<Site>> DeletedRows(Guid siteId, CancellationToken cancellationToken)
    {
        List<Site> rows;

        using (context.SoftDelete.IncludeDeleted())
        {
            rows = await context.Sites.OfSite(siteId).ToListAsync(cancellationToken);
        }

        var deleted = new List<Site>();

        foreach (var row in rows)
        {
            if (await context.Entry(row).IsDeletedAsync(cancellationToken))
            {
                deleted.Add(row);
            }
        }

        return deleted;
    }

    private async Task<SiteSaveResult> AddVersion(
        Guid siteId,
        long previousVersion,
        string title,
        string slug,
        string content,
        bool publish,
        bool hideTitle,
        IReadOnlyList<ISiteCommands.SiteSectionDraft> sections,
        CancellationToken cancellationToken)
    {
        if (await context.IsSiteSlugTakenAsync(versions, siteId, slug, cancellationToken))
        {
            return new SiteSaveResult.SlugTaken();
        }

        if (!await HasImages(sections, cancellationToken)
            || !await context.HasContentChildren<SiteSection>(siteId, SiteSectionConfiguration.SiteIdProperty,
                sections.Select(section => section.Id), cancellationToken))
        {
            return new SiteSaveResult.UnknownImage();
        }

        var now = timeProvider.GetUtcNow();
        var version = previousVersion + 1;
        var site = new Site(title, slug, content, now, hideTitle);

        await context.Sites.AddAsync(site, cancellationToken);

        var entry = context.Entry(site);
        entry.Property<Guid>(SiteConfiguration.SiteIdProperty).CurrentValue = siteId;
        entry.Property<long>(SiteConfiguration.VersionProperty).CurrentValue = version;
        entry.Property<DateTimeOffset?>(SiteConfiguration.PublishedAtProperty).CurrentValue = publish ? now : null;

        foreach (var (section, position) in sections.Select((section, position) => (section, position)))
        {
            await AddSection(new VersionedKey<Guid>(siteId, version), section, position, cancellationToken);
        }

        if (context.ContentLocaleSpecified)
        {
            await context.StageContentLocale(nameof(Site), siteId, context.SelectedContentLocale, cancellationToken);
        }

        var result = await Save(
            new VersionedKey<Guid>(siteId, version),
            cancellationToken,
            publish ? () => SetActive(siteId, version, cancellationToken) : null);

        if (result is SiteSaveResult.Saved)
        {
            await files.AttachSiteImagesOf(
                VfsTypes.Sites,
                siteId,
                [content, .. sections.Select(section => section.Content)],
                ImageKeysOf(sections),
                cancellationToken);
        }

        return result;
    }

    private async Task AddSection(
        VersionedKey<Guid> site,
        ISiteCommands.SiteSectionDraft draft,
        int position,
        CancellationToken cancellationToken)
    {
        var section = new SiteSection(draft.Theme, draft.Title, draft.Content, draft.ImageAlt, draft.ImagePosition);

        await context.SiteSections.AddAsync(section, cancellationToken);

        var entry = context.Entry(section);
        entry.StampVersionedKey(draft.Id ?? Guid.CreateVersion7(), site.Version);
        entry.Property<Guid>(SiteSectionConfiguration.SiteIdProperty).CurrentValue = site.Key;
        entry.Property<Guid?>(SiteSectionConfiguration.ImageIdProperty).CurrentValue =
            draft.ImageId.Match<Guid?>(() => null, imageId => imageId);
        entry.Property<int>(SiteSectionConfiguration.PositionProperty).CurrentValue = position;
    }

    private static IReadOnlyList<Guid> ImageKeysOf(IEnumerable<ISiteCommands.SiteSectionDraft> sections) =>
        sections.SelectMany(section => section.ImageId.AsEnumerable()).Distinct().ToList();

    private async Task<bool> HasImages(
        IReadOnlyList<ISiteCommands.SiteSectionDraft> sections,
        CancellationToken cancellationToken)
    {
        var wanted = ImageKeysOf(sections);

        var found = await context.VfsFiles
            .InSameKindAs(VfsTypes.Sites)
            .OfKeys(wanted)
            .Select(file => EF.Property<Guid>(file, VersionedColumns.Key))
            .Distinct()
            .CountAsync(cancellationToken);

        return found == wanted.Count;
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
