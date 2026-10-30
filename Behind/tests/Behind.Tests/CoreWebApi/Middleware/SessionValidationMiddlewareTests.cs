using System.Security.Claims;
using Behind.Core.Database.Queries;
using Behind.Core.Database.Sqlite;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.Middleware;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;

namespace Behind.Tests.CoreWebApi.Middleware;

public sealed class SessionValidationMiddlewareTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static ValidSession NewSession(
        DateTimeOffset? issuedAt = null,
        TimeSpan? accessDuration = null,
        TimeSpan? refreshDuration = null)
    {
        var now = issuedAt ?? DateTimeOffset.UtcNow;
        return new ValidSession(
            AccessToken.Create(new GuestUser(None), now, accessDuration ?? TimeSpan.FromMinutes(15)),
            RefreshToken.Create(new GuestUser(None), now, refreshDuration ?? TimeSpan.FromDays(7)),
            "10.0.0.1",
            "test-agent");
    }

    private async Task<Guid> SeedAsync(ValidSession session, DateTimeOffset? invalidatedAt = null)
    {
        await using var seed = _factory.CreateContext();
        return await DatabaseSeedHelper.SeedSessionAsync(
            seed,
            session,
            DomainConstants.SystemUserId,
            invalidatedAt: invalidatedAt);
    }

    private static DefaultHttpContext ContextFor(params Claim[] claims) =>
        new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };

    private static Claim[] ClaimsFor(Guid sessionId, TokenNonce nonce) =>
    [
        new(SessionClaimTypes.SessionId, sessionId.ToString()),
        new(SessionClaimTypes.TokenNonce, nonce)
    ];

    private async Task<bool> InvokeAsync(DefaultHttpContext context)
    {
        var reachedEndpoint = false;
        var middleware = new SessionValidationMiddleware(_ =>
        {
            reachedEndpoint = true;
            return Task.CompletedTask;
        }, TimeProvider.System);

        await using var db = _factory.CreateContext();
        await middleware.InvokeAsync(context, new SessionQueries(db));

        return reachedEndpoint;
    }

    [Fact]
    public async Task InvokeAsync_AnonymousRequest_PassesThrough()
    {
        var context = new DefaultHttpContext();

        Assert.True(await InvokeAsync(context));
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ValidSession_PassesThrough()
    {
        var session = NewSession();
        var id = await SeedAsync(session);
        var context = ContextFor(ClaimsFor(id, session.AccessToken.Nonce));

        Assert.True(await InvokeAsync(context));
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedWithoutSessionClaims_Returns401()
    {
        var context = ContextFor(new Claim(ClaimTypes.Email, "user@test.com"));

        Assert.False(await InvokeAsync(context));
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_MalformedSessionClaim_Returns401()
    {
        var context = ContextFor(
            new Claim(SessionClaimTypes.SessionId, "not-a-guid"),
            new Claim(SessionClaimTypes.TokenNonce, Guid.NewGuid().ToString()));

        Assert.False(await InvokeAsync(context));
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_UnknownSession_Returns401()
    {
        var context = ContextFor(ClaimsFor(Guid.NewGuid(), Guid.NewGuid()));

        Assert.False(await InvokeAsync(context));
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_InvalidatedSession_Returns401()
    {
        var session = NewSession();
        var id = await SeedAsync(session, DateTimeOffset.UtcNow);
        var context = ContextFor(ClaimsFor(id, session.AccessToken.Nonce));

        Assert.False(await InvokeAsync(context));
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Contains("invalidated", context.Response.Headers.WWWAuthenticate.ToString());
    }

    [Fact]
    public async Task InvokeAsync_NonceDiffersFromSessionAccessToken_Returns401()
    {
        var id = await SeedAsync(NewSession());
        var context = ContextFor(ClaimsFor(id, Guid.NewGuid()));

        Assert.False(await InvokeAsync(context));
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ExpiredAccessToken_Returns401()
    {
        var session = NewSession(DateTimeOffset.UtcNow.AddHours(-2), TimeSpan.FromMinutes(15));
        var id = await SeedAsync(session);
        var context = ContextFor(ClaimsFor(id, session.AccessToken.Nonce));

        Assert.False(await InvokeAsync(context));
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ExpiredRefreshToken_Returns401()
    {
        var session = NewSession(refreshDuration: TimeSpan.FromSeconds(-1));
        var id = await SeedAsync(session);
        var context = ContextFor(ClaimsFor(id, session.AccessToken.Nonce));

        Assert.False(await InvokeAsync(context));
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Rejection_SetsBearerChallenge()
    {
        var context = ContextFor(ClaimsFor(Guid.NewGuid(), Guid.NewGuid()));

        await InvokeAsync(context);

        var challenge = context.Response.Headers.WWWAuthenticate.ToString();
        Assert.StartsWith("Bearer error=\"invalid_token\"", challenge);
    }
}
