using Behind.Core.Database;
using Behind.Core.Database.Queries;
using Behind.Tests.Helpers;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class AuditLogEntryQueriesTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static IAuditLogEntryQueries.RequestAuditLog TodayRequest(
        Flipper? flipper = null,
        Filter<AuditLogEntry>? filter = null,
        OrderByCollection? orderBy = null) =>
        new(
            FromDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            ToDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Filter: filter ?? new EmptyFilter<AuditLogEntry>(),
            Flipper: flipper ?? new Flipper(1, 20),
            OrderByCollection: orderBy ?? new OrderByCollection());

    // -----------------------------------------------------------------------
    // Empty results
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAuditLog_NoEntries_ReturnsNone()
    {
        await using var ctx = _factory.CreateContext();
        var queries = new AuditLogEntryQueries(ctx);
        var result = await queries.GetAuditLog(TodayRequest());

        var isNone = true;
        result.Match(() => { }, _ => isNone = false);
        Assert.True(isNone);
    }

    // -----------------------------------------------------------------------
    // Single entry
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAuditLog_OneEntry_ReturnsSomeWithTotal1()
    {
        await using var seed = _factory.CreateContext();
        await DatabaseSeedHelper.SeedAuditLogEntryAsync(seed);

        await using var ctx = _factory.CreateContext();
        var result = await new AuditLogEntryQueries(ctx).GetAuditLog(TodayRequest());

        result.Match(
            () => Assert.Fail("Expected Some"),
            paged =>
            {
                Assert.Equal(1, paged.Total);
                paged.Results.Match(
                    () => Assert.Fail("Expected results"),
                    items => Assert.Single(items));
            });
    }

    // -----------------------------------------------------------------------
    // User resolution in query results
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAuditLog_EntryWithHumanUser_ItemHasHumanUserWithEmail()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "q-human@test.com");
        await DatabaseSeedHelper.SeedAuditLogEntryAsync(seed, userId: userId);

        await using var ctx = _factory.CreateContext();
        var result = await new AuditLogEntryQueries(ctx).GetAuditLog(TodayRequest());

        result.Match(() => Assert.Fail("Expected Some"), paged =>
        {
            paged.Results.Match(() => Assert.Fail("Expected results"), items =>
            {
                var item = items.Single();
                var hu = Assert.IsType<HumanUser>(item.User);
                Assert.Contains(hu.Emails, e => e.Value == "q-human@test.com");
            });
        });
    }

    [Fact]
    public async Task GetAuditLog_EntryWithSystemUser_ItemHasSystemUser()
    {
        await using var seed = _factory.CreateContext();
        await DatabaseSeedHelper.SeedAuditLogEntryAsync(seed, userId: DomainConstants.SystemUserId);

        await using var ctx = _factory.CreateContext();
        var result = await new AuditLogEntryQueries(ctx).GetAuditLog(TodayRequest());

        result.Match(() => Assert.Fail("Expected Some"), paged =>
        {
            paged.Results.Match(() => Assert.Fail("Expected results"), items =>
                Assert.IsType<SystemUser>(items.Single().User));
        });
    }

    [Fact]
    public async Task GetAuditLog_EntryWithBehalfOf_ItemHasSomeBehalfOf()
    {
        await using var seed = _factory.CreateContext();
        var behalfId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "q-behalf@test.com");
        await DatabaseSeedHelper.SeedAuditLogEntryAsync(seed, behalfOfId: behalfId);

        await using var ctx = _factory.CreateContext();
        var result = await new AuditLogEntryQueries(ctx).GetAuditLog(TodayRequest());

        result.Match(() => Assert.Fail("Expected Some"), paged =>
        {
            paged.Results.Match(() => Assert.Fail("Expected results"), items =>
            {
                var item = items.Single();
                var hasBehalf = false;
                item.BehalfOf.Match(() => { }, _ => hasBehalf = true);
                Assert.True(hasBehalf);
            });
        });
    }

    // -----------------------------------------------------------------------
    // Date range filtering
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAuditLog_EntryOutsideDateRange_IsExcluded()
    {
        var oldTimestamp = DateTimeOffset.UtcNow.AddDays(-30);
        await using var seed = _factory.CreateContext();
        await DatabaseSeedHelper.SeedAuditLogEntryAsync(seed, timestamp: oldTimestamp);

        // Request covers yesterday to tomorrow — old entry should be excluded
        await using var ctx = _factory.CreateContext();
        var result = await new AuditLogEntryQueries(ctx).GetAuditLog(TodayRequest());

        var isNone = true;
        result.Match(() => { }, _ => isNone = false);
        Assert.True(isNone);
    }

    [Fact]
    public async Task GetAuditLog_MixedEntries_OnlyReturnsInRangeEntries()
    {
        var nowTs = DateTimeOffset.UtcNow;
        var oldTs = DateTimeOffset.UtcNow.AddDays(-30);

        await using var seed = _factory.CreateContext();
        await DatabaseSeedHelper.SeedAuditLogEntryAsync(seed, timestamp: nowTs);
        await DatabaseSeedHelper.SeedAuditLogEntryAsync(seed, timestamp: oldTs);

        await using var ctx = _factory.CreateContext();
        var result = await new AuditLogEntryQueries(ctx).GetAuditLog(TodayRequest());

        result.Match(() => Assert.Fail("Expected Some"), paged =>
        {
            Assert.Equal(1, paged.Total);
        });
    }

    // -----------------------------------------------------------------------
    // Pagination
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAuditLog_Pagination_Page2Returns2Items()
    {
        var baseTs = DateTimeOffset.UtcNow;
        await using var seed = _factory.CreateContext();
        for (int i = 0; i < 5; i++)
            await DatabaseSeedHelper.SeedAuditLogEntryAsync(
                seed, timestamp: baseTs.AddSeconds(i));

        var request = TodayRequest(flipper: new Flipper(2, 2));
        await using var ctx = _factory.CreateContext();
        var result = await new AuditLogEntryQueries(ctx).GetAuditLog(request);

        result.Match(() => Assert.Fail("Expected Some"), paged =>
        {
            Assert.Equal(5, paged.Total);
            Assert.Equal(2, paged.Page);
            paged.Results.Match(
                () => Assert.Fail("Expected results"),
                items => Assert.Equal(2, items.Count()));
        });
    }

    [Fact]
    public async Task GetAuditLog_LastPage_ReturnsRemainingItems()
    {
        var baseTs = DateTimeOffset.UtcNow;
        await using var seed = _factory.CreateContext();
        for (int i = 0; i < 5; i++)
            await DatabaseSeedHelper.SeedAuditLogEntryAsync(
                seed, timestamp: baseTs.AddSeconds(i));

        var request = TodayRequest(flipper: new Flipper(3, 2));
        await using var ctx = _factory.CreateContext();
        var result = await new AuditLogEntryQueries(ctx).GetAuditLog(request);

        result.Match(() => Assert.Fail("Expected Some"), paged =>
        {
            paged.Results.Match(
                () => Assert.Fail("Expected results"),
                items => Assert.Single(items));
        });
    }

    // -----------------------------------------------------------------------
    // Default ordering (by Timestamp) is applied when OrderByCollection is empty
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAuditLog_DefaultOrder_ReturnsEntriesOrderedByTimestamp()
    {
        var baseTs = DateTimeOffset.UtcNow;
        await using var seed = _factory.CreateContext();
        // Insert in reverse chronological order
        await DatabaseSeedHelper.SeedAuditLogEntryAsync(seed, timestamp: baseTs.AddSeconds(2));
        await DatabaseSeedHelper.SeedAuditLogEntryAsync(seed, timestamp: baseTs);
        await DatabaseSeedHelper.SeedAuditLogEntryAsync(seed, timestamp: baseTs.AddSeconds(1));

        await using var ctx = _factory.CreateContext();
        var result = await new AuditLogEntryQueries(ctx).GetAuditLog(TodayRequest());

        result.Match(() => Assert.Fail("Expected Some"), paged =>
        {
            paged.Results.Match(() => Assert.Fail("Expected results"), items =>
            {
                var list = items.ToList();
                Assert.Equal(3, list.Count);
                // After default ordering by Timestamp ascending, the ticks should be non-decreasing
                for (int i = 1; i < list.Count; i++)
                    Assert.True(list[i].Timestamp >= list[i - 1].Timestamp);
            });
        });
    }
    
    [Fact]
    public async Task SeedAuditLogEntryAsync_WithSystemUserId_SeedsRequiredBuiltInUser()
    {
        Guid id;

        await using (var seed = _factory.CreateContext())
        {
            id = await DatabaseSeedHelper.SeedAuditLogEntryAsync(
                seed,
                userId: DomainConstants.SystemUserId);
        }

        await using var ctx = _factory.CreateContext();
        var result = await ((IRepository<AuditLogEntry, Guid>)ctx).TryFindAsync(id);

        result.Match(
            () => Assert.Fail("Expected audit log entry."),
            entry => Assert.IsType<SystemUser>(entry.User));
    }
}
