using Behind.Common;
using Behind.Common.Database.Raw;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using System.Runtime.CompilerServices;
using System.Data.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class RawDatabaseQueries(
    AbstractCoreContext context,
    HybridCache? cache = null) : IRawDatabaseQueries
{
    private static readonly ConditionalWeakTable<DbConnection, MemoryDatabaseIdentity> MemoryDatabases = new();

    public async Task<IReadOnlyList<IRawDatabaseQueries.RawTableListing>> ListTables(
        CancellationToken cancellationToken = default)
    {
        var tables = context.RawTables.ToArray();
        var canCache = cache is not null
            && context.Database.CurrentTransaction is null
            && System.Transactions.Transaction.Current is null
            && !context.ChangeTracker.HasChanges();
        var revision = canCache ? await context.AuditLogEntries.LongCountAsync(cancellationToken) : 0;
        var key = canCache ? CountsKey(tables, revision) : string.Empty;
        long[]? counts = null;
        if (canCache)
        {
            counts = await cache!.GetOrCreateAsync<long[]?>(
                key,
                static _ => throw new InvalidOperationException("Cache-only lookups must not invoke a factory."),
                new HybridCacheEntryOptions { Flags = HybridCacheEntryFlags.DisableUnderlyingData },
                cancellationToken: cancellationToken);
        }

        if (counts is null)
        {
            counts = new long[tables.Length];
            for (var index = 0; index < tables.Length; index++)
            {
                counts[index] = await context.CountRawRowsAsync(tables[index], None, cancellationToken);
            }

            if (canCache && revision == await context.AuditLogEntries.LongCountAsync(cancellationToken))
            {
                await cache!.SetAsync(key, counts, cancellationToken: cancellationToken);
            }
        }

        if (counts.Length != tables.Length || counts.Any(count => count < 0))
        {
            throw new InvalidOperationException("Invalid cached table-count aggregate.");
        }

        return tables.Select((table, index) =>
            new IRawDatabaseQueries.RawTableListing(table, counts[index], context.IsReadOnly(table))).ToArray();
    }

    private string CountsKey(IReadOnlyList<RawTable> tables, long revision)
    {
        var connection = context.Database.GetDbConnection();
        var identity = System.Text.Json.JsonSerializer.Serialize(new
        {
            Provider = context.Database.ProviderName,
            connection.DataSource,
            connection.Database,
            MemoryDatabase = connection.DataSource is ":memory:" or ""
                ? MemoryDatabases.GetValue(connection, static _ => new MemoryDatabaseIdentity(Guid.NewGuid())).Id
                : Guid.Empty,
            Tables = tables.Select(table => new { table.Schema, table.Name }).ToArray()
        });
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return $"query:raw-table-counts:v1:{digest}:{revision}";
    }

    private sealed record MemoryDatabaseIdentity(Guid Id);

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
