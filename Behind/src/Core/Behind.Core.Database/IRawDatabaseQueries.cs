using Behind.Common;
using Behind.Common.Database.Raw;

namespace Behind.Core.Database;

public interface IRawDatabaseQueries
{
    public sealed record RawTableListing(RawTable Table, long Rows, bool IsReadOnly);

    Task<IReadOnlyList<RawTableListing>> ListTables(CancellationToken cancellationToken = default);

    Task<Option<PagedResult<RawRow>>> ListRows(
        string table,
        RawRowQuery query,
        CancellationToken cancellationToken = default);
}
