using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase.Repository;

public sealed class WallOfShameTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<Guid> SeedUserAsync(string email)
    {
        await using var ctx = _factory.CreateContext();
        return await DatabaseSeedHelper.SeedHumanUserAsync(ctx, email);
    }

    private async Task BanAsync(Guid userId, Guid givenBy, string reason)
    {
        await using var ctx = _factory.CreateAuditedContext(AuditActor.ForUser(givenBy));
        await new WallOfShameCommands(ctx).Ban(userId, givenBy, new Reason(reason));
    }

    private async Task<Option<IWallOfShameQueries.ActiveBan>> FindBanAsync(Guid userId)
    {
        await using var ctx = _factory.CreateContext();
        return await new WallOfShameQueries(ctx).FindActiveBan(userId, Now);
    }

    [Fact]
    public async Task Ban_MakesTheBanActiveWithItsReason()
    {
        var admin = await SeedUserAsync("admin@test.com");
        var user = await SeedUserAsync("user@test.com");

        await BanAsync(user, admin, "spam");

        Assert.Equal(Some(new IWallOfShameQueries.ActiveBan("spam", None)), await FindBanAsync(user));
        Assert.Equal(None, await FindBanAsync(admin));
    }

    [Fact]
    public async Task Ban_AgainReplacesThePreviousReason()
    {
        var admin = await SeedUserAsync("admin@test.com");
        var user = await SeedUserAsync("user@test.com");

        await BanAsync(user, admin, "first");
        await BanAsync(user, admin, "second");

        Assert.Equal(Some(new IWallOfShameQueries.ActiveBan("second", None)), await FindBanAsync(user));
        await using var ctx = _factory.CreateContext();
        var page = await new WallOfShameQueries(ctx).GetActive(new Flipper(1, 10), Now);
        Assert.Equal(1, page.Total);
    }

    [Fact]
    public async Task Unban_EndsTheBanAndKeepsTheRowForAuditing()
    {
        var admin = await SeedUserAsync("admin@test.com");
        var user = await SeedUserAsync("user@test.com");
        await BanAsync(user, admin, "spam");

        await using (var ctx = _factory.CreateAuditedContext(AuditActor.ForUser(admin)))
        {
            Assert.True(await new WallOfShameCommands(ctx).Unban(user));
        }

        Assert.Equal(None, await FindBanAsync(user));
        await using var verify = _factory.CreateContext();
        Assert.True(await verify.WallOfShameEntries.IgnoreQueryFilters().AnyAsync());
    }

    [Fact]
    public async Task Unban_WithoutActiveBan_ReturnsFalse()
    {
        var user = await SeedUserAsync("user@test.com");

        await using var ctx = _factory.CreateContext();

        Assert.False(await new WallOfShameCommands(ctx).Unban(user));
    }

    [Fact]
    public async Task FindActiveBan_IgnoresExpiredEntriesAndReturnsFutureExpiry()
    {
        var admin = await SeedUserAsync("admin@test.com");
        var expired = await SeedUserAsync("expired@test.com");
        var temporary = await SeedUserAsync("temporary@test.com");

        await using (var seed = _factory.CreateContext())
        {
            await DatabaseSeedHelper.SeedWallOfShameEntryAsync(seed, expired, admin, "old", Now.AddHours(-1));
            await DatabaseSeedHelper.SeedWallOfShameEntryAsync(seed, temporary, admin, "cooling off", Now.AddDays(2));
        }

        Assert.Equal(None, await FindBanAsync(expired));
        Assert.Equal(
            Some(new IWallOfShameQueries.ActiveBan("cooling off", Some(Now.AddDays(2)))),
            await FindBanAsync(temporary));
    }

    [Fact]
    public async Task GetActive_ListsNewestFirstWithDisplayNamesAndPages()
    {
        var admin = await SeedUserAsync("admin@test.com");
        var first = await SeedUserAsync("first@test.com");
        var second = await SeedUserAsync("second@test.com");
        var expired = await SeedUserAsync("expired@test.com");
        await BanAsync(first, admin, "one");
        await BanAsync(second, admin, "two");

        await using (var seed = _factory.CreateContext())
        {
            await DatabaseSeedHelper.SeedWallOfShameEntryAsync(seed, expired, admin, "gone", Now.AddMinutes(-1));
        }

        await using var ctx = _factory.CreateContext();
        var page = await new WallOfShameQueries(ctx).GetActive(new Flipper(1, 1), Now);
        var next = await new WallOfShameQueries(ctx).GetActive(new Flipper(2, 1), Now);

        Assert.Equal((2L, 2L, 1), (page.Total, page.PageCount, page.PageSize));
        Assert.Equal(["two"], page.Results.Match(() => [], items => items.Select(i => i.Reason).ToList()));
        Assert.Equal(["one"], next.Results.Match(() => [], items => items.Select(i => i.Reason).ToList()));
        Assert.Equal(second.ToUserLabel("Test User"), page.Results.Match(() => "", items => items.First().DisplayName));
        Assert.Equal(second, page.Results.Match(() => Guid.Empty, items => items.First().Id));
    }

    [Fact]
    public async Task GetActive_WithoutBans_ReturnsAnEmptyPage()
    {
        await using var ctx = _factory.CreateContext();

        var page = await new WallOfShameQueries(ctx).GetActive(new Flipper(1, 10), Now);

        Assert.Equal(0, page.Total);
        Assert.Equal(None, page.Results);
    }

    [Fact]
    public async Task GetActive_UsesAuditCreationBeforeIdForOrdering()
    {
        var admin = await SeedUserAsync("admin@test.com");
        var older = await SeedUserAsync("older@test.com");
        var newer = await SeedUserAsync("newer@test.com");

        async Task Add(Guid id, Guid userId, string reason, DateTimeOffset at)
        {
            await using var context = _factory.CreateAuditedContext(
                AuditActor.ForUser(admin), new FixedTimeProvider(at));
            var entry = new WallOfShameEntry(new Reason(reason));
            await context.WallOfShameEntries.AddAsync(entry);
            context.Entry(entry).Property<Guid>("Id").CurrentValue = id;
            context.Entry(entry).Property<Guid>("UserId").CurrentValue = userId;
            context.Entry(entry).Property<Guid>("GivenById").CurrentValue = admin;
            await context.SaveChangesAsync();
        }

        await Add(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), older, "older", Now);
        await Add(Guid.Parse("11111111-1111-1111-1111-111111111111"), newer, "newer", Now.AddMinutes(1));
        await using var read = _factory.CreateContext();
        var page = await new WallOfShameQueries(read).GetActive(new Flipper(1, 1), Now.AddMinutes(2));

        Assert.Equal(["newer"], page.Results.Match(() => [], rows => rows.Select(row => row.Reason).ToList()));
    }
}
