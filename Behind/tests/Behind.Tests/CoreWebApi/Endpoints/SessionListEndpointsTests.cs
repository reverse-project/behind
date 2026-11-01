using System.Security.Claims;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Behind.Tests.Helpers;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class SessionListEndpointsTests
{
    private static readonly Guid CurrentId = Guid.NewGuid();
    private static readonly Guid OtherId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    private static ISessionQueries.SessionSummary Summary(
        Guid id,
        bool signedOut = false,
        Option<DateTimeOffset> signedOutAt = default) =>
        new(
            id,
            Now.AddHours(-3),
            Now.AddHours(-1),
            Now.AddDays(1),
            "203.0.113.7",
            "agent",
            signedOut,
            signedOutAt);

    private static DefaultHttpContext SignedIn() =>
        new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("session_id", CurrentId.ToString())],
                "test"))
        };

    private static PagedResult<ISessionQueries.SessionSummary> Page(
        long total,
        long page,
        long pageCount,
        params ISessionQueries.SessionSummary[] items) =>
        new(total, page, pageCount, 10, items.Length > 0 ? Some<IEnumerable<ISessionQueries.SessionSummary>>(items) : None);

    private static ISessionQueries Queries(
        ISessionQueries.UserSessions sessions,
        Flipper? active = null,
        Flipper? ended = null)
    {
        var queries = Substitute.For<ISessionQueries>();
        queries.TryFindByIdWithUserId(CurrentId, Arg.Any<CancellationToken>())
            .Returns(Some<(Session, Guid)>((
                new ValidSession(
                    AccessToken.Create(new GuestUser(None)),
                    RefreshToken.Create(new GuestUser(None)),
                    "10.0.0.1",
                    "test-agent"),
                UserId)));
        queries.GetUserSessions(
                UserId,
                Now,
                active ?? new Flipper(1, SessionListEndpoints.DefaultPageSize),
                ended ?? new Flipper(1, SessionListEndpoints.DefaultPageSize),
                Arg.Any<CancellationToken>())
            .Returns(sessions);
        return queries;
    }

    [Fact]
    public async Task Get_ReturnsActiveAndEndedSessionsAndMarksTheCurrentOne()
    {
        var signedOutAt = Now.AddHours(-2);
        var sessions = new ISessionQueries.UserSessions(
            Page(12, 1, 2, Summary(CurrentId), Summary(OtherId)),
            Page(
                2,
                1,
                1,
                Summary(Guid.NewGuid(), signedOut: true, signedOutAt: Some(signedOutAt)),
                Summary(Guid.NewGuid())));

        var result = await SessionListEndpoints.Get(SignedIn(), Queries(sessions), new FixedTimeProvider(Now));

        var body = Assert.IsType<JsonHttpResult<SessionListResponse>>(result).Value!;
        Assert.Equal(CurrentId, body.CurrentSessionId);
        Assert.Equal([true, false], body.Active.Items.Select(s => s.IsCurrent));
        Assert.All(body.Active.Items, s => Assert.Null(s.EndedAt));
        Assert.Equal("203.0.113.7", body.Active.Items[0].RemoteAddress);
        Assert.Equal("agent", body.Active.Items[0].UserAgent);
        Assert.Equal([true, false], body.Ended.Items.Select(s => s.SignedOut));
        Assert.Equal(signedOutAt, body.Ended.Items[0].EndedAt);
        Assert.Equal(Now.AddDays(1), body.Ended.Items[1].EndedAt);
        Assert.Equal((12L, 1L, 2L, 10), (body.Active.Total, body.Active.Page, body.Active.PageCount, body.Active.PageSize));
        Assert.Equal((2L, 1L, 1L, 10), (body.Ended.Total, body.Ended.Page, body.Ended.PageCount, body.Ended.PageSize));
    }

    [Fact]
    public async Task Get_DisablesCaching()
    {
        var context = SignedIn();

        await SessionListEndpoints.Get(
            context,
            Queries(new ISessionQueries.UserSessions(Page(0, 1, 0), Page(0, 1, 0))),
            new FixedTimeProvider(Now));

        Assert.Equal("no-store", context.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task Get_WithoutSession_ReturnsUnauthorized()
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal() };

        var result = await SessionListEndpoints.Get(
            context,
            Substitute.For<ISessionQueries>(),
            new FixedTimeProvider(Now));

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task Get_EmptyPages_ReturnEmptyItems()
    {
        var sessions = new ISessionQueries.UserSessions(Page(0, 1, 0), Page(0, 1, 0));

        var result = await SessionListEndpoints.Get(SignedIn(), Queries(sessions), new FixedTimeProvider(Now));

        var body = Assert.IsType<JsonHttpResult<SessionListResponse>>(result).Value!;
        Assert.Empty(body.Active.Items);
        Assert.Empty(body.Ended.Items);
    }

    [Fact]
    public async Task Get_PassesRequestedPagesAndClampsThePageSize()
    {
        var sessions = new ISessionQueries.UserSessions(Page(0, 3, 0), Page(0, 2, 0));
        var queries = Queries(sessions, new Flipper(3, SessionListEndpoints.MaxPageSize), new Flipper(2, SessionListEndpoints.MaxPageSize));

        var result = await SessionListEndpoints.Get(
            SignedIn(),
            queries,
            new FixedTimeProvider(Now),
            activePage: 3,
            endedPage: 2,
            pageSize: 100000);

        var body = Assert.IsType<JsonHttpResult<SessionListResponse>>(result).Value!;
        Assert.Equal(3, body.Active.Page);
        Assert.Equal(2, body.Ended.Page);
    }
}
