using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

internal sealed record DownloadFolderRow(Guid Key, long Version, Guid? ParentId, string Slug, string DisplayName);

internal sealed record DownloadRow(
    Guid Key,
    long Version,
    Guid FolderId,
    Guid FileId,
    Guid UploadedById,
    string DisplayName);

internal static class DownloadQueryExtensions
{
    extension(IQueryable<DownloadFolder> folders)
    {
        public IQueryable<DownloadFolderRow> ToRows() =>
            folders.Select(folder => new DownloadFolderRow(
                EF.Property<Guid>(folder, VersionedColumns.Key),
                EF.Property<long>(folder, VersionedColumns.Version),
                EF.Property<Guid?>(folder, DownloadFolderConfiguration.ParentIdProperty),
                folder.Slug,
                folder.DisplayName));
    }

    extension(IQueryable<Download> downloads)
    {
        public IQueryable<DownloadRow> ToRows() =>
            downloads.Select(download => new DownloadRow(
                EF.Property<Guid>(download, VersionedColumns.Key),
                EF.Property<long>(download, VersionedColumns.Version),
                EF.Property<Guid>(download, DownloadConfiguration.FolderIdProperty),
                EF.Property<Guid>(download, DownloadConfiguration.FileIdProperty),
                EF.Property<Guid>(download, DownloadConfiguration.UploadedByIdProperty),
                download.DisplayName));
    }

    extension(IEnumerable<DownloadFolderRow> rows)
    {
        public IReadOnlyList<DownloadFolderRow> Reachable()
        {
            var byParent = rows.ToLookup(row => row.ParentId);

            return Descend(byParent, null).ToList();
        }

        public IReadOnlySet<Guid> DescendantKeysOf(Guid folderId)
        {
            var byParent = rows.ToLookup(row => row.ParentId);

            return Descend(byParent, folderId).Select(row => row.Key).ToHashSet();
        }

        public IReadOnlyList<DownloadFolderRow> InDisplayOrder() =>
            rows
                .OrderBy(row => row.DisplayName.ToLowerInvariant())
                .ThenBy(row => row.Key)
                .ToList();
    }

    extension(AbstractCoreContext context)
    {
        public IQueryable<DownloadFolder> LatestFoldersIn(DeletionState state) =>
            context.DownloadFolders
                .InDeletionState(context, AbstractCoreContext.DeletionRecorded, state)
                .LatestOf(context.DownloadFolders);

        public IQueryable<Download> LatestDownloadsIn(DeletionState state) =>
            context.Downloads
                .InDeletionState(context, AbstractCoreContext.DeletionRecorded, state)
                .LatestOf(context.Downloads);

        public async Task<IReadOnlyList<DownloadFolderRow>> ReachableFoldersAsync(
            CancellationToken cancellationToken)
        {
            var rows = await context.LatestFoldersIn(DeletionState.Active)
                .ToRows()
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return rows.Reachable().InDisplayOrder();
        }

        public async Task<IReadOnlyList<IDownloadQueries.DownloadInfo>> ToInfosAsync(
            IQueryable<Download> downloads,
            CancellationToken cancellationToken)
        {
            var rows = await downloads.ToRows().AsNoTracking().ToListAsync(cancellationToken);

            var users = await context.DisplayNamesAsync(
                rows.Select(row => row.UploadedById).Distinct().ToList(),
                cancellationToken);

            var times = await context.AuditTimesAsync<Download>(
                rows.ToDictionary(row => row.Key, row => row.Version),
                cancellationToken);

            var files = (await context.VfsFiles
                    .OfKeys(rows.Select(row => row.FileId).Distinct().ToList())
                    .ToFilesAsync(cancellationToken))
                .Latest()
                .ToDictionary(file => file.Id.Key);

            return rows
                .Where(row => files.ContainsKey(row.FileId))
                .OrderBy(row => row.DisplayName.ToLowerInvariant())
                .ThenBy(row => row.Key)
                .Select(row => new IDownloadQueries.DownloadInfo(
                    row.Key,
                    row.FolderId,
                    row.DisplayName,
                    files[row.FileId].FileName,
                    files[row.FileId].ContentType,
                    files[row.FileId].Length,
                    users.GetValueOrDefault(row.UploadedById, string.Empty),
                    times.TryGetValue(row.Key, out var time) ? time.CreatedAt : FunctionalExtensions.None))
                .ToList();
        }
    }

    private static IEnumerable<DownloadFolderRow> Descend(ILookup<Guid?, DownloadFolderRow> byParent, Guid? parent) =>
        byParent[parent].SelectMany(child => new[] { child }.Concat(Descend(byParent, child.Key)));
}
