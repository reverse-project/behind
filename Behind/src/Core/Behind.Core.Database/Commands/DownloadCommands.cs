using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public sealed class DownloadCommands(AbstractCoreContext context, IVfsCommands files) : IDownloadCommands
{
    public async Task<DownloadSaveResult> Add(
        Guid folderId,
        Guid uploadedById,
        IDownloadCommands.FileDraft draft,
        CancellationToken cancellationToken = default)
    {
        if (!await IsFolderActive(folderId, cancellationToken))
        {
            return new DownloadSaveResult.FolderNotFound();
        }

        var fileId = await files.Add(
            new IVfsCommands.Upload(VfsTypes.Downloads, draft.FileName, draft.ContentType, draft.Content),
            cancellationToken);

        var saved = await AddVersion(
            Guid.CreateVersion7(),
            0,
            new DownloadRow(
                Guid.Empty,
                0,
                folderId,
                fileId,
                uploadedById,
                draft.DisplayName.ToDownloadDisplayName(draft.FileName)),
            cancellationToken);

        if (saved is not DownloadSaveResult.Saved added)
        {
            await files.Delete([fileId], CancellationToken.None);

            return saved;
        }

        await files.Attach(VfsTypes.Downloads, added.Id.Key, [fileId], cancellationToken);

        return saved;
    }

    public async Task<DownloadSaveResult> Rename(
        Guid downloadId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var latest = (await context.LatestDownloadsIn(DeletionState.Active)
                .OfKey(downloadId)
                .ToRows()
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .Head();

        return await latest.Match(
            () => Task.FromResult<DownloadSaveResult>(new DownloadSaveResult.NotFound()),
            row => AddVersion(
                downloadId,
                row.Version,
                row with { DisplayName = displayName.ToDownloadDisplayName(row.DisplayName) },
                cancellationToken));
    }

    public async Task<bool> Delete(Guid downloadId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.MarkSoftDeletedAsync(context.Downloads.OfKey(downloadId), cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return deleted > 0;
    }

    public async Task<DownloadSaveResult> Restore(Guid downloadId, CancellationToken cancellationToken = default)
    {
        if (!await context.HasDeletedRowsAsync(context.Downloads.OfKey(downloadId), cancellationToken))
        {
            return new DownloadSaveResult.NotFound();
        }

        var latest = await LatestOf(downloadId, cancellationToken);

        if (!await IsFolderActive(latest.FolderId, cancellationToken))
        {
            return new DownloadSaveResult.FolderDeleted();
        }

        await context.MarkRestoredAsync(context.Downloads.OfKey(downloadId), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new DownloadSaveResult.Saved(new VersionedKey<Guid>(downloadId, latest.Version));
    }

    public async Task<HardDeleteResult> HardDelete(Guid downloadId, CancellationToken cancellationToken = default)
    {
        var fileIds = await FileIdsOf(downloadId, cancellationToken);
        var deleted = await context.MarkHardDeletedAsync(context.Downloads.OfKey(downloadId), cancellationToken);

        if (deleted == 0)
        {
            return HardDeleteResult.NotFound;
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

    private async Task<DownloadSaveResult> AddVersion(
        Guid key,
        long previousVersion,
        DownloadRow row,
        CancellationToken cancellationToken)
    {
        var id = new VersionedKey<Guid>(key, previousVersion + 1);
        var download = new Download(row.DisplayName);

        await context.Downloads.AddAsync(download, cancellationToken);

        var entry = context.Entry(download);
        entry.StampVersionedKey(id.Key, id.Version);
        entry.Property<Guid>(DownloadConfiguration.FolderIdProperty).CurrentValue = row.FolderId;
        entry.Property<Guid>(DownloadConfiguration.FileIdProperty).CurrentValue = row.FileId;
        entry.Property<Guid>(DownloadConfiguration.UploadedByIdProperty).CurrentValue = row.UploadedById;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();

            return new DownloadSaveResult.VersionConflict();
        }

        return new DownloadSaveResult.Saved(id);
    }

    private Task<bool> IsFolderActive(Guid folderId, CancellationToken cancellationToken) =>
        context.LatestFoldersIn(DeletionState.Active).OfKey(folderId).AnyAsync(cancellationToken);

    private async Task<DownloadRow> LatestOf(Guid downloadId, CancellationToken cancellationToken)
    {
        using var scope = context.SoftDelete.IncludeDeleted();

        return await context.Downloads
            .OfKey(downloadId)
            .OrderByDescending(download => EF.Property<long>(download, VersionedColumns.Version))
            .ToRows()
            .FirstAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> FileIdsOf(Guid downloadId, CancellationToken cancellationToken)
    {
        using var scope = context.SoftDelete.IncludeDeleted();

        return await context.Downloads
            .OfKey(downloadId)
            .Select(download => EF.Property<Guid>(download, DownloadConfiguration.FileIdProperty))
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
