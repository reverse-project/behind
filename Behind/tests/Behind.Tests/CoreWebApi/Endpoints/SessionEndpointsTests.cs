using System.Security.Claims;
using Behind.Core.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class SessionEndpointsTests
{
    private static readonly JwtSettings Jwt = new("issuer", "audience", new string('k', 64));
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly HumanUser Human = new("Jane", [new Email("jane@test.com")], None);
    private static readonly FixedTimeProvider Time = new(Now);

    private static IOptions<SecurityConfiguration> Security(Option<JwtSettings> jwt) =>
        Options.Create(new SecurityConfiguration(false, None, jwt, None));

    private static ValidSession NewSession(
        TimeSpan accessAge = default,
        TimeSpan? accessDuration = null,
        TimeSpan? refreshDuration = null) =>
        new(
            Human,
            AccessToken.Create(Human, Now - accessAge, accessDuration ?? TimeSpan.FromMinutes(30)),
            RefreshToken.Create(Human, Now - TimeSpan.FromHours(2), refreshDuration ?? TimeSpan.FromDays(30)),
            "10.0.0.1",
            "test-agent");

    private static ISessionQueries QueriesFor(Session session)
    {
        var queries = Substitute.For<ISessionQueries>();
        queries.TryFindByIdWithUserId(SessionId, Arg.Any<CancellationToken>())
            .Returns(Some<(Session, Guid)>((session, UserId)));
        return queries;
    }

    private static IRoleQueries Roles(params Role[] roles)
    {
        var queries = Substitute.For<IRoleQueries>();
        queries.GetRolesByUserId(UserId, Arg.Any<CancellationToken>()).Returns(roles);
        return queries;
    }

    private static ISessionCommands Commands(bool renewed = true)
    {
        var commands = Substitute.For<ISessionCommands>();
        commands.RenewAccessToken(
                Arg.Any<Guid>(),
                Arg.Any<TokenNonce>(),
                Arg.Any<AccessToken>(),
                Arg.Any<CancellationToken>())
            .Returns(renewed);
        return commands;
    }

    private static DefaultHttpContext AnonymousContext(string? refreshToken = null)
    {
        var context = new DefaultHttpContext();
        if (refreshToken is not null)
        {
            context.Request.Headers[SessionEndpoints.RefreshTokenHeader] = refreshToken;
        }

        return context;
    }

    private static DefaultHttpContext AuthenticatedContext(TokenNonce nonce, string? refreshToken = null)
    {
        var context = AnonymousContext(refreshToken);
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(SessionClaimTypes.SessionId, SessionId.ToString()),
            new Claim(SessionClaimTypes.TokenNonce, nonce)
        ], "test"));
        return context;
    }

    private static string RefreshJwt(ValidSession session) =>
        session.RefreshToken.ToJwtToken(Human.ToApplicationUser(UserId), SessionId, Jwt);

    private static string AccessJwt(ValidSession session) =>
        session.AccessToken.ToJwtToken(Human.ToApplicationUser(UserId), SessionId, Jwt);

    private static Task<IResult> Get(
        HttpContext context,
        ISessionQueries queries,
        ISessionCommands? commands = null,
        IRoleQueries? roles = null,
        Option<JwtSettings>? jwt = null,
        TimeProvider? time = null) =>
        SessionEndpoints.Get(
            context,
            Security(jwt ?? Some(Jwt)),
            queries,
            commands ?? Commands(),
            roles ?? Roles(),
            time ?? Time);

    private static SessionResponse BodyOf(IResult result) =>
        Assert.IsType<JsonHttpResult<SessionResponse>>(result).Value!;

    [Fact]
    public async Task Get_ValidAccessToken_ReturnsSessionUserRolesAndLength()
    {
        var session = NewSession(accessAge: TimeSpan.FromMinutes(10));
        var roles = Roles(
            new UserRole("admin", Some<IEnumerable<Grant>>([new Grant("read"), new Grant("write")])),
            new UserRole("viewer"));
        var commands = Commands();

        var body = BodyOf(await Get(
            AuthenticatedContext(session.AccessToken.Nonce),
            QueriesFor(session),
            commands,
            roles));

        Assert.Equal(SessionId, body.SessionId);
        Assert.Null(body.Refreshed);
        Assert.Equal(UserId, body.User.Id);
        Assert.Equal("Jane", body.User.DisplayName);
        Assert.Equal(["jane@test.com"], body.User.Emails);
        Assert.Equal(["admin", "viewer"], body.User.Roles.Select(r => r.Name));
        Assert.Equal(["read", "write"], body.User.Roles[0].Grants);
        Assert.Empty(body.User.Roles[1].Grants);
        Assert.Equal(TimeSpan.FromHours(2), body.Session.Length);
        Assert.Equal(session.RefreshToken.IssuedAt, body.Session.StartedAt);
        Assert.Equal(TimeSpan.FromMinutes(20), body.Session.AccessTokenRemaining);
        Assert.Equal(session.AccessToken.ExpiresAt, body.Session.AccessTokenExpiresAt);
        Assert.Equal(session.RefreshToken.ExpiresAt, body.Session.ExpiresAt);
        Assert.Equal(session.RefreshToken.ExpiresAt - Now, body.Session.Remaining);
        Assert.Equal("10.0.0.1", body.Session.RemoteAddress);
        Assert.Equal("test-agent", body.Session.UserAgent);
        await commands.DidNotReceiveWithAnyArgs().RenewAccessToken(default, default!, default!, default);
    }

    [Fact]
    public async Task Get_ValidAccessToken_DisablesCaching()
    {
        var session = NewSession();
        var context = AuthenticatedContext(session.AccessToken.Nonce);

        await Get(context, QueriesFor(session));

        Assert.Equal("no-store", context.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task Get_ExpiredAccessTokenWithValidRefreshToken_RenewsAccessToken()
    {
        var session = NewSession(accessAge: TimeSpan.FromHours(1));
        var commands = Commands();
        var roles = Roles(new UserRole("admin"));

        var body = BodyOf(await Get(
            AnonymousContext(RefreshJwt(session)),
            QueriesFor(session),
            commands,
            roles));

        var renewed = Assert.IsType<RefreshedAccessToken>(body.Refreshed);
        var captured = Assert.IsType<AccessToken>(
            commands.ReceivedCalls().Single().GetArguments()[2]);
        await commands.Received(1).RenewAccessToken(
            SessionId,
            session.AccessToken.Nonce,
            captured,
            Arg.Any<CancellationToken>());
        Assert.NotEqual(session.AccessToken.Nonce, captured.Nonce);
        Assert.Equal(Now + DomainConstants.AccessTokenDuration, captured.ExpiresAt);
        Assert.Equal(captured.ExpiresAt, renewed.Expires);
        var claims = await Jwt.ReadSessionClaims(renewed.AccessToken);
        Assert.Equal(
            new Behind.Core.WebApi.Middleware.SessionClaims(SessionId, captured.Nonce),
            claims.Match(() => throw new Xunit.Sdk.XunitException("None"), c => c));
        Assert.Equal(captured.ExpiresAt, body.Session.AccessTokenExpiresAt);
        Assert.Equal(TimeSpan.FromMinutes(30), body.Session.AccessTokenRemaining);
        Assert.Equal(["admin"], body.User.Roles.Select(r => r.Name));
    }

    [Fact]
    public async Task Get_AuthenticatedButSessionAccessTokenExpired_RenewsWithRefreshToken()
    {
        var session = NewSession(accessAge: TimeSpan.FromHours(1));

        var body = BodyOf(await Get(
            AuthenticatedContext(session.AccessToken.Nonce, RefreshJwt(session)),
            QueriesFor(session)));

        Assert.NotNull(body.Refreshed);
    }

    [Fact]
    public async Task Get_ExpiredAccessTokenAndRenewalLost_ReturnsConflict()
    {
        var session = NewSession(accessAge: TimeSpan.FromHours(1));

        var result = await Get(
            AnonymousContext(RefreshJwt(session)),
            QueriesFor(session),
            Commands(renewed: false));

        Assert.IsType<Conflict>(result);
    }

    [Fact]
    public async Task Get_NoTokens_ReturnsUnauthorized()
    {
        var session = NewSession();

        Assert.IsType<UnauthorizedHttpResult>(await Get(AnonymousContext(), QueriesFor(session)));
    }

    [Fact]
    public async Task Get_GarbageRefreshToken_ReturnsUnauthorized()
    {
        var session = NewSession(accessAge: TimeSpan.FromHours(1));

        Assert.IsType<UnauthorizedHttpResult>(
            await Get(AnonymousContext("not-a-token"), QueriesFor(session)));
    }

    [Fact]
    public async Task Get_AccessTokenUsedAsRefreshToken_ReturnsUnauthorized()
    {
        var session = NewSession(accessDuration: TimeSpan.FromMinutes(30));
        var commands = Commands();

        var result = await Get(AnonymousContext(AccessJwt(session)), QueriesFor(session), commands);

        Assert.IsType<UnauthorizedHttpResult>(result);
        await commands.DidNotReceiveWithAnyArgs().RenewAccessToken(default, default!, default!, default);
    }

    [Fact]
    public async Task Get_ExpiredRefreshToken_ReturnsUnauthorized()
    {
        var session = NewSession(accessAge: TimeSpan.FromHours(1), refreshDuration: TimeSpan.FromHours(3));
        var commands = Commands();

        var result = await Get(
            AnonymousContext(RefreshJwt(session)),
            QueriesFor(session),
            commands,
            time: new FixedTimeProvider(Now.AddHours(2)));

        Assert.IsType<UnauthorizedHttpResult>(result);
        await commands.DidNotReceiveWithAnyArgs().RenewAccessToken(default, default!, default!, default);
    }

    [Fact]
    public async Task Get_RefreshTokenOfReplacedSession_ReturnsUnauthorized()
    {
        var session = NewSession(accessAge: TimeSpan.FromHours(1));
        var other = NewSession(accessAge: TimeSpan.FromHours(1));

        Assert.IsType<UnauthorizedHttpResult>(
            await Get(AnonymousContext(RefreshJwt(other)), QueriesFor(session)));
    }

    [Fact]
    public async Task Get_InvalidatedSession_ReturnsUnauthorized()
    {
        var session = NewSession();
        var invalid = new InvalidSession(Some<User>(Human), Some(Now));
        var commands = Commands();

        var result = await Get(
            AuthenticatedContext(session.AccessToken.Nonce, RefreshJwt(session)),
            QueriesFor(invalid),
            commands);

        Assert.IsType<UnauthorizedHttpResult>(result);
        await commands.DidNotReceiveWithAnyArgs().RenewAccessToken(default, default!, default!, default);
    }

    [Fact]
    public async Task Get_UnknownSession_ReturnsUnauthorized()
    {
        var queries = Substitute.For<ISessionQueries>();
        queries.TryFindByIdWithUserId(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(None);

        Assert.IsType<UnauthorizedHttpResult>(
            await Get(AuthenticatedContext(Guid.NewGuid()), queries));
    }

    [Fact]
    public async Task Get_SessionWithoutHumanUser_ReturnsUnauthorized()
    {
        var session = NewSession() with { User = new GuestUser(None) };

        Assert.IsType<UnauthorizedHttpResult>(
            await Get(AuthenticatedContext(session.AccessToken.Nonce), QueriesFor(session)));
    }

    [Fact]
    public async Task Get_WithoutJwtSettings_ReturnsServerError()
    {
        var session = NewSession();

        var result = await Get(
            AuthenticatedContext(session.AccessToken.Nonce),
            QueriesFor(session),
            jwt: None);

        Assert.IsType<InternalServerError>(result);
    }
}
