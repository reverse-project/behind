using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Tests.Helpers;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class SessionQueriesUserSessionsTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static ValidSession SessionStartedAt(
        TimeSpan age,
        TimeSpan refreshDuration,
        string address = "10.0.0.1",
        string agent = "test-agent") =>
        new(
            AccessToken.Create(new GuestUser(None), Now - age, TimeSpan.FromMinutes(30)),
            RefreshToken.Create(new GuestUser(None), Now - age, refreshDuration),
            address,
            agent);

    private async Task<Guid> SeedUserAsync(string email)
    {
        await using var ctx = _factory.CreateContext();
        return await DatabaseSeedHelper.SeedHumanUserAsync(ctx, email);
    }

    private async Task<Guid> SeedSessionAsync(Guid userId, ValidSession session)
    {
        await using var ctx = _factory.CreateContext();
        return await new SessionCommands(ctx).CreateForUserId(userId, session);
    }

    private async Task SignOutAsync(Guid sessionId, Guid userId, DateTimeOffset at)
    {
        await using var ctx = _factory.CreateContext();
        await new SessionCommands(ctx).InvalidateSession(sessionId, userId, at);
    }

    private async Task<ISessionQueries.UserSessions> QueryAsync(
        Guid userId,
        Flipper? active = null,
        Flipper? ended = null)
    {
        await using var ctx = _factory.CreateContext();
        return await new SessionQueries(ctx).GetUserSessions(
            userId,
            Now,
            active ?? new Flipper(1, 50),
            ended ?? new Flipper(1, 50));
    }

    private static IReadOnlyList<ISessionQueries.SessionSummary> Items(PagedResult<ISessionQueries.SessionSummary> page) =>
        page.Results.Match(() => (IReadOnlyList<ISessionQueries.SessionSummary>)[], results => results.ToList());

    [Fact]
    public async Task GetUserSessions_SplitsActiveSignedOutAndExpired()
    {
        var userId = await SeedUserAsync("split@test.com");
        var active = await SeedSessionAsync(userId, SessionStartedAt(TimeSpan.FromHours(1), TimeSpan.FromDays(30)));
        var signedOut = await SeedSessionAsync(userId, SessionStartedAt(TimeSpan.FromHours(3), TimeSpan.FromDays(30)));
        var expired = await SeedSessionAsync(userId, SessionStartedAt(TimeSpan.FromDays(2), TimeSpan.FromDays(1)));
        await SignOutAsync(signedOut, userId, Now.AddHours(-2));

        var sessions = await QueryAsync(userId);

        Assert.Equal([active], Items(sessions.Active).Select(s => s.Id));
        Assert.Equal([signedOut, expired], Items(sessions.Ended).Select(s => s.Id));
        Assert.True(Items(sessions.Ended)[0].SignedOut);
        Assert.Equal(Some(Now.AddHours(-2)), Items(sessions.Ended)[0].SignedOutAt);
        Assert.False(Items(sessions.Ended)[1].SignedOut);
        Assert.Equal(None, Items(sessions.Ended)[1].SignedOutAt);
    }

    [Fact]
    public async Task GetUserSessions_ReturnsAddressUserAgentAndTimes()
    {
        var userId = await SeedUserAsync("details@test.com");
        var session = SessionStartedAt(TimeSpan.FromHours(1), TimeSpan.FromDays(30), "203.0.113.7", "Mozilla/5.0 Test");
        await SeedSessionAsync(userId, session);

        var summary = Assert.Single(Items((await QueryAsync(userId)).Active));

        Assert.Equal("203.0.113.7", summary.RemoteAddress.Value);
        Assert.Equal("Mozilla/5.0 Test", summary.UserAgent.Value);
        Assert.Equal(session.RefreshToken.IssuedAt, summary.StartedAt);
        Assert.Equal(session.AccessToken.IssuedAt, summary.LastActiveAt);
        Assert.Equal(session.RefreshToken.ExpiresAt, summary.ExpiresAt);
        Assert.False(summary.SignedOut);
    }

    [Fact]
    public async Task GetUserSessions_OrdersNewestFirst()
    {
        var userId = await SeedUserAsync("order@test.com");
        var oldest = await SeedSessionAsync(userId, SessionStartedAt(TimeSpan.FromHours(5), TimeSpan.FromDays(30)));
        var newest = await SeedSessionAsync(userId, SessionStartedAt(TimeSpan.FromHours(1), TimeSpan.FromDays(30)));
        var middle = await SeedSessionAsync(userId, SessionStartedAt(TimeSpan.FromHours(3), TimeSpan.FromDays(30)));

        var sessions = await QueryAsync(userId);

        Assert.Equal([newest, middle, oldest], Items(sessions.Active).Select(s => s.Id));
    }

    [Fact]
    public async Task GetUserSessions_OnlyIncludesTheRequestedUsersSessions()
    {
        var userId = await SeedUserAsync("mine@test.com");
        var otherId = await SeedUserAsync("other@test.com");
        var mine = await SeedSessionAsync(userId, SessionStartedAt(TimeSpan.FromHours(1), TimeSpan.FromDays(30)));
        await SeedSessionAsync(otherId, SessionStartedAt(TimeSpan.FromHours(1), TimeSpan.FromDays(30)));

        var sessions = await QueryAsync(userId);

        Assert.Equal([mine], Items(sessions.Active).Select(s => s.Id));
        Assert.Empty(Items(sessions.Ended));
    }

    [Fact]
    public async Task GetUserSessions_PagesEndedSessionsNewestFirstWithTotals()
    {
        var userId = await SeedUserAsync("paging@test.com");
        var ids = new List<Guid>();
        foreach (var days in new[] { 7, 6, 5, 4, 3 })
        {
            ids.Add(await SeedSessionAsync(userId, SessionStartedAt(TimeSpan.FromDays(days), TimeSpan.FromDays(1))));
        }

        var first = (await QueryAsync(userId, ended: new Flipper(1, 2))).Ended;
        var second = (await QueryAsync(userId, ended: new Flipper(2, 2))).Ended;
        var third = (await QueryAsync(userId, ended: new Flipper(3, 2))).Ended;

        Assert.Equal([ids[4], ids[3]], Items(first).Select(s => s.Id));
        Assert.Equal([ids[2], ids[1]], Items(second).Select(s => s.Id));
        Assert.Equal([ids[0]], Items(third).Select(s => s.Id));
        Assert.All([first, second, third], page =>
        {
            Assert.Equal(5, page.Total);
            Assert.Equal(3, page.PageCount);
            Assert.Equal(2, page.PageSize);
        });
        Assert.Equal([1L, 2L, 3L], new[] { first.Page, second.Page, third.Page });
    }

    [Fact]
    public async Task GetUserSessions_PagesActiveAndEndedIndependently()
    {
        var userId = await SeedUserAsync("independent@test.com");
        var actives = new List<Guid>();
        foreach (var hours in new[] { 5, 3, 1 })
        {
            actives.Add(await SeedSessionAsync(userId, SessionStartedAt(TimeSpan.FromHours(hours), TimeSpan.FromDays(30))));
        }

        await SeedSessionAsync(userId, SessionStartedAt(TimeSpan.FromDays(3), TimeSpan.FromDays(1)));

        var sessions = await QueryAsync(userId, active: new Flipper(2, 2), ended: new Flipper(1, 2));

        Assert.Equal([actives[0]], Items(sessions.Active).Select(s => s.Id));
        Assert.Equal(3, sessions.Active.Total);
        Assert.Equal(2, sessions.Active.PageCount);
        Assert.Single(Items(sessions.Ended));
        Assert.Equal(1, sessions.Ended.PageCount);
    }

    [Fact]
    public async Task GetUserSessions_PageBeyondTheEnd_HasNoResultsButKeepsTotals()
    {
        var userId = await SeedUserAsync("beyond@test.com");
        await SeedSessionAsync(userId, SessionStartedAt(TimeSpan.FromHours(1), TimeSpan.FromDays(30)));

        var sessions = await QueryAsync(userId, active: new Flipper(5, 10));

        Assert.Empty(Items(sessions.Active));
        Assert.Equal(1, sessions.Active.Total);
        Assert.Equal(1, sessions.Active.PageCount);
    }

    [Fact]
    public async Task GetUserSessions_UserWithoutSessions_ReturnsEmptyLists()
    {
        var userId = await SeedUserAsync("none@test.com");

        var sessions = await QueryAsync(userId);

        Assert.Empty(Items(sessions.Active));
        Assert.Empty(Items(sessions.Ended));
    }
}
