using Behind.Common;
using Behind.Common.Database;
using Behind.Common.Storage;
using Behind.Core.Domain;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class VfsQueries(AbstractCoreContext context, IContentStore store) : IVfsQueries
{
    public async Task<Option<IVfsQueries.Content>> Open(
        IReadOnlyList<string> types,
        Guid key,
        CancellationToken cancellationToken = default)
    {
        var files = await context.VfsFiles
            .InTypes(types)
            .OfKey(key)
            .ToFilesAsync(cancellationToken);

        return await files.Latest().Head().Match(
            () => Task.FromResult<Option<IVfsQueries.Content>>(None),
            async file => (await store.Read(file.StorageKey, cancellationToken))
                .Map(bytes => new IVfsQueries.Content(file, bytes)));
    }
}
