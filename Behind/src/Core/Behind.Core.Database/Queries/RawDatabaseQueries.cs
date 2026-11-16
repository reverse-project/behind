using Behind.Common;
using Behind.Common.Database.Raw;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class RawDatabaseQueries(AbstractCoreContext context) : IRawDatabaseQueries
{
    public async Task<IReadOnlyList<IRawDatabaseQueries.RawTableListing>> ListTables(
        CancellationToken cancellationToken = default)
    {
        var listings = new List<IRawDatabaseQueries.RawTableListing>();

        foreach (var table in context.RawTables)
        {
            var rows = await context.CountRawRowsAsync(table, None, cancellationToken);
            listings.Add(new IRawDatabaseQueries.RawTableListing(table, rows, context.IsReadOnly(table)));
        }

        return listings;
    }

    public Task<Option<PagedResult<RawRow>>> ListRows(
        string table,
        RawRowQuery query,
        CancellationToken cancellationToken = default) =>
        context.FindRawTable(table).Match(
            () => Task.FromResult<Option<PagedResult<RawRow>>>(None),
            async raw =>
            {
                var page = await context.ListRawRowsAsync(raw, query, cancellationToken);

                return Some(page with { Results = page.Results.Map(rows => rows.Select(raw.Mask)) });
            });
}
