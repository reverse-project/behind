using System.Data.Common;
using System.Text.Json;
using Behind.Common.Database.Raw;
using Behind.Core.Database.Auditing;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.Database.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreDatabase;

public sealed class RawDatabaseCachingTests
{
    [Fact]
    public async Task ReusesOnlyCountsAndCommittedCommandsSelectANewRevision()
    {
        using var factory = new TestDbContextFactory();
        using var services = Services();
        var cache = services.GetRequiredService<HybridCache>();
        await using var shared = factory.CreateContext();
        var counter = new ReadCounter();
        await using var read = new CoreContextSqlite(new DbContextOptionsBuilder<CoreContextSqlite>()
            .UseSqlite(shared.Database.GetDbConnection()).AddInterceptors(counter).Options);
        var queries = new RawDatabaseQueries(read, cache);
        var baseline = await queries.ListTables();
        counter.Reads = 0;
        var repeated = await queries.ListTables();
        Assert.Equal(1, counter.Reads);
        Assert.Equal(baseline.Select(item => item.Rows), repeated.Select(item => item.Rows));

        await using var write = factory.CreateAuditedContext(AuditActor.Anonymous);
        var commands = new RawDatabaseCommands(write, new FixedAuditActorProvider(AuditActor.Anonymous), TimeProvider.System);
        var id = Guid.NewGuid();
        var values = new Dictionary<string, JsonElement>
        {
            ["Id"] = JsonSerializer.SerializeToElement(id),
            ["Name"] = JsonSerializer.SerializeToElement("private-person@example.test")
        };
        Assert.IsType<RawWriteResult.Saved>(await commands.Insert("Grants", values));
        counter.Reads = 0;
        var inserted = await queries.ListTables();
        Assert.True(counter.Reads > 1);
        Assert.Equal(Count(baseline, "Grants") + 1, Count(inserted, "Grants"));
        Assert.IsType<RawWriteResult.Saved>(await commands.Update("Grants",
            new Dictionary<string, JsonElement> { ["Id"] = JsonSerializer.SerializeToElement(id) },
            new Dictionary<string, JsonElement> { ["Name"] = JsonSerializer.SerializeToElement("Changed") }));
        counter.Reads = 0;
        Assert.Equal(Count(inserted, "Grants"), Count(await queries.ListTables(), "Grants"));
        Assert.True(counter.Reads > 1);
        Assert.IsType<RawWriteResult.Deleted>(await commands.Delete("Grants",
            new Dictionary<string, JsonElement> { ["Id"] = JsonSerializer.SerializeToElement(id) }));
        Assert.Equal(Count(baseline, "Grants"), Count(await queries.ListTables(), "Grants"));
    }

    [Fact]
    public async Task RolledBackWritesCannotPoisonTheCommittedCache()
    {
        using var factory = new TestDbContextFactory();
        using var services = Services();
        var cache = services.GetRequiredService<HybridCache>();
        await using var read = factory.CreateContext();
        var baseline = await new RawDatabaseQueries(read, cache).ListTables();
        await using (var write = factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            await using var transaction = await write.Database.BeginTransactionAsync();
            write.Grants.Add(new Grant("NeverCached"));
            await write.SaveChangesAsync();
            var pending = await new RawDatabaseQueries(write, cache).ListTables();
            Assert.Equal(Count(baseline, "Grants") + 1, Count(pending, "Grants"));
            await transaction.RollbackAsync();
        }

        Assert.Equal(Count(baseline, "Grants"), Count(await new RawDatabaseQueries(read, cache).ListTables(), "Grants"));
    }

    [Fact]
    public async Task SeparateMemoryDatabasesCannotShareCachedCounts()
    {
        using var first = new TestDbContextFactory();
        using var second = new TestDbContextFactory();
        using var services = Services();
        var cache = services.GetRequiredService<HybridCache>();
        await using var one = first.CreateContext();
        await using var two = second.CreateContext();
        one.Grants.Add(new Grant("DifferentDatabase"));
        await one.SaveChangesAsync();
        var countsOne = await new RawDatabaseQueries(one, cache).ListTables();
        var countsTwo = await new RawDatabaseQueries(two, cache).ListTables();
        Assert.Equal(Count(countsTwo, "Grants") + 1, Count(countsOne, "Grants"));
    }

    private static long Count(IReadOnlyList<Behind.Core.Database.IRawDatabaseQueries.RawTableListing> rows, string name) =>
        rows.Single(row => row.Table.Name == name).Rows;

    private static ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        return services.BuildServiceProvider();
    }

    private sealed class ReadCounter : DbCommandInterceptor
    {
        public int Reads;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Reads);
            return ValueTask.FromResult(result);
        }
    }
}
