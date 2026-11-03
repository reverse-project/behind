using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public static class VfsQueryExtensions
{
    extension(IQueryable<VfsFile> files)
    {
        public IQueryable<VfsFile> InSameKindAs(string type)
        {
            var types = VfsTypes.SameKindAs(type);

            return files.Where(file => types.Contains(file.Type));
        }

        public async Task<IReadOnlyList<VfsFile>> ToFilesAsync(CancellationToken cancellationToken = default)
        {
            var rows = await files
                .Select(file => new
                {
                    File = file,
                    Key = EF.Property<Guid>(file, VersionedColumns.Key),
                    Version = EF.Property<long>(file, VersionedColumns.Version)
                })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return rows
                .Select(row => row.File with { Id = new VersionedKey<Guid>(row.Key, row.Version) })
                .ToList();
        }
    }

    extension(IEnumerable<VfsFile> files)
    {
        public IEnumerable<VfsFile> Latest() =>
            files
                .GroupBy(file => file.Id.Key)
                .Select(versions => versions.MaxBy(file => file.Id.Version)!);
    }
}
