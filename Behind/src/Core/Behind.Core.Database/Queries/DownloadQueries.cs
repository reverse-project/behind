using System.Linq.Expressions;
using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class DownloadQueries(AbstractCoreContext context, IVfsQueries files) : IDownloadQueries
{
    public async Task<IReadOnlyList<IDownloadQueries.DownloadFolderInfo>> ListFolders(
        CancellationToken cancellationToken = default) =>
        (await context.ReachableFoldersAsync(cancellationToken)).Select(ToInfo).ToList();

    public async Task<Option<IReadOnlyList<IDownloadQueries.DownloadInfo>>> ListFiles(
        Guid folderId,
        CancellationToken cancellationToken = default)
    {
        var folders = await context.ReachableFoldersAsync(cancellationToken);

        if (folders.All(folder => folder.Key != folderId))
        {
            return None;
        }

        var infos = await context.ToInfosAsync(
            context.LatestDownloadsIn(DeletionState.Active).Where(OfFolder(folderId)),
            cancellationToken);

        return Some(infos);
    }

    public async Task<IDownloadQueries.DeletedDownloads> ListDeleted(CancellationToken cancellationToken = default)
    {
        using var scope = context.SoftDelete.IncludeDeleted();

        var folders = await context.LatestFoldersIn(DeletionState.Deleted)
            .ToRows()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var infos = await context.ToInfosAsync(context.LatestDownloadsIn(DeletionState.Deleted), cancellationToken);

        return new IDownloadQueries.DeletedDownloads(folders.InDisplayOrder().Select(ToInfo).ToList(), infos);
    }

    public async Task<Option<IVfsQueries.Content>> Open(
        Guid downloadId,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.LatestDownloadsIn(DeletionState.Active)
            .OfKey(downloadId)
            .ToRows()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var reachable = await context.ReachableFoldersAsync(cancellationToken);

        return await rows
            .Where(row => reachable.Any(folder => folder.Key == row.FolderId))
            .Head()
            .Match(
                () => Task.FromResult<Option<IVfsQueries.Content>>(None),
                row => files.Open(VfsTypes.DownloadFiles, row.FileId, cancellationToken));
    }

    private static Expression<Func<Download, bool>> OfFolder(Guid folderId) =>
        download => EF.Property<Guid>(download, DownloadConfiguration.FolderIdProperty) == folderId;

    private static IDownloadQueries.DownloadFolderInfo ToInfo(DownloadFolderRow row) =>
        new(row.Key, row.ParentId.ToOption(), row.Slug, row.DisplayName);
}
