using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public sealed class DownloadFolderCommands(AbstractCoreContext context, IVfsCommands files) : IDownloadFolderCommands
{
    private sealed record Latest(long Version, Guid? ParentId, string Slug);

    public async Task<DownloadFolderSaveResult> Create(
        Option<Guid> parentId,
        IDownloadFolderCommands.FolderDraft draft,
        CancellationToken cancellationToken = default)
    {
        if (await parentId.Match(
                () => Task.FromResult(false),
                async parent => !await IsActive(parent, cancellationToken)))
        {
            return new DownloadFolderSaveResult.ParentNotFound();
        }

        return await AddVersion(Guid.CreateVersion7(), 0, parentId.ToNullable(), draft, cancellationToken);
    }

    public async Task<DownloadFolderSaveResult> Update(
        Guid folderId,
        IDownloadFolderCommands.FolderDraft draft,
        CancellationToken cancellationToken = default)
    {
        var latest = (await context.LatestFoldersIn(DeletionState.Active)
                .OfKey(folderId)
                .ToRows()
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .Head();

        return await latest.Match(
            () => Task.FromResult<DownloadFolderSaveResult>(new DownloadFolderSaveResult.NotFound()),
            row => AddVersion(folderId, row.Version, row.ParentId, draft, cancellationToken));
    }

    public async Task<bool> Delete(Guid folderId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.MarkSoftDeletedAsync(context.DownloadFolders.OfKey(folderId), cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return deleted > 0;
    }

    public async Task<DownloadFolderSaveResult> Restore(
        Guid folderId,
        CancellationToken cancellationToken = default)
    {
        if (!await context.HasDeletedRowsAsync(context.DownloadFolders.OfKey(folderId), cancellationToken))
        {
            return new DownloadFolderSaveResult.NotFound();
        }

        var latest = await LatestOf(folderId, cancellationToken);

        if (latest.ParentId is { } parentId && !await IsActive(parentId, cancellationToken))
        {
            return new DownloadFolderSaveResult.ParentDeleted();
        }

        if (await IsSlugTaken(folderId, latest.ParentId, latest.Slug, cancellationToken))
        {
            return new DownloadFolderSaveResult.SlugTaken();
        }

        await context.MarkRestoredAsync(context.DownloadFolders.OfKey(folderId), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new DownloadFolderSaveResult.Saved(new VersionedKey<Guid>(folderId, latest.Version));
    }

    public async Task<HardDeleteResult> HardDelete(Guid folderId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.MarkHardDeletedAsync(context.DownloadFolders.OfKey(folderId), cancellationToken);

        if (deleted == 0)
        {
            return HardDeleteResult.NotFound;
        }

        IReadOnlyList<Guid> fileIds;

        using (context.SoftDelete.IncludeDeleted())
        {
            fileIds = await HardDeleteContents(folderId, cancellationToken);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await files.Delete(fileIds, cancellationToken);

            return HardDeleteResult.Deleted;
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();

            return HardDeleteResult.Rejected;
        }
    }

    private async Task<IReadOnlyList<Guid>> HardDeleteContents(Guid folderId, CancellationToken cancellationToken)
    {
        var everyFolder = await context.DownloadFolders.ToRows().AsNoTracking().ToListAsync(cancellationToken);
        var descendants = everyFolder.DescendantKeysOf(folderId);
        var removed = descendants.Append(folderId).ToList();

        var folders = await context.DownloadFolders.OfKeys(descendants.ToList()).ToListAsync(cancellationToken);

        var downloads = await context.Downloads
            .Where(download => removed.Contains(
                EF.Property<Guid>(download, DownloadConfiguration.FolderIdProperty)))
            .ToListAsync(cancellationToken);

        folders.ForEach(context.HardDelete);
        downloads.ForEach(context.HardDelete);

        return downloads
            .Select(download => context.Entry(download).Property<Guid>(DownloadConfiguration.FileIdProperty).CurrentValue)
            .Distinct()
            .ToList();
    }

    private async Task<DownloadFolderSaveResult> AddVersion(
        Guid key,
        long previousVersion,
        Guid? parentId,
        IDownloadFolderCommands.FolderDraft draft,
        CancellationToken cancellationToken)
    {
        if (await IsSlugTaken(key, parentId, draft.Slug, cancellationToken))
        {
            return new DownloadFolderSaveResult.SlugTaken();
        }

        var id = new VersionedKey<Guid>(key, previousVersion + 1);
        var folder = new DownloadFolder(draft.Slug, draft.DisplayName);

        await context.DownloadFolders.AddAsync(folder, cancellationToken);

        var entry = context.Entry(folder);
        entry.StampVersionedKey(id.Key, id.Version);
        entry.Property<Guid?>(DownloadFolderConfiguration.ParentIdProperty).CurrentValue = parentId;
        if (context.ContentLocaleSpecified)
        {
            await context.StageContentLocale(nameof(DownloadFolder), key, context.SelectedContentLocale, cancellationToken);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();

            return new DownloadFolderSaveResult.VersionConflict();
        }

        return new DownloadFolderSaveResult.Saved(id);
    }

    private Task<bool> IsActive(Guid folderId, CancellationToken cancellationToken) =>
        context.LatestFoldersIn(DeletionState.Active).OfKey(folderId).AnyAsync(cancellationToken);

    private async Task<Latest> LatestOf(Guid folderId, CancellationToken cancellationToken)
    {
        using var scope = context.SoftDelete.IncludeDeleted();

        var row = await context.DownloadFolders
            .OfKey(folderId)
            .OrderByDescending(folder => EF.Property<long>(folder, VersionedColumns.Version))
            .ToRows()
            .FirstAsync(cancellationToken);

        return new Latest(row.Version, row.ParentId, row.Slug);
    }

    private async Task<bool> IsSlugTaken(
        Guid folderId,
        Guid? parentId,
        string slug,
        CancellationToken cancellationToken)
    {
        var lowered = slug.ToLowerInvariant();
        var locale = context.ContentLocaleSpecified
            ? context.SelectedContentLocale
            : await context.FindContentLocale(nameof(DownloadFolder), folderId, cancellationToken);

        return await context.InContentLocale(context.LatestFoldersIn(DeletionState.Active), nameof(DownloadFolder), locale)
            .AnyAsync(
                folder => folder.Slug.ToLower() == lowered
                          && EF.Property<Guid?>(folder, DownloadFolderConfiguration.ParentIdProperty) == parentId
                          && EF.Property<Guid>(folder, VersionedColumns.Key) != folderId,
                cancellationToken);
    }
}
