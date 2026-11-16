using Behind.Common;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IVfsQueries
{
    public sealed record Content(VfsFile File, byte[] Bytes);

    Task<Option<Content>> Open(
        IReadOnlyList<string> types,
        Guid key,
        CancellationToken cancellationToken = default);
}
