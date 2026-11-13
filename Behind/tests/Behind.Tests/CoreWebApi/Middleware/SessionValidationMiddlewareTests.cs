using System.Security.Claims;
using System.Globalization;
using Behind.Common.Logging;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.Database.Sqlite;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.Middleware;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Behind.Tests.CoreWebApi.Middleware;

public sealed class SessionValidationMiddlewareTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly CapturingLogger<SessionValidationMiddleware> _logger = new();
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

    private async Task<bool> InvokeAsync(DefaultHttpContext context, RequestDelegate? endpoint = null)
    {
        var reachedEndpoint = false;
        var middleware = new SessionValidationMiddleware(async http =>
        {
            reachedEndpoint = true;
            if (endpoint is not null)
            {
                await endpoint(http);
            }
        }, TimeProvider.System, _logger);

        await using var db = _factory.CreateContext();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Frontend:Site:DefaultLocale"] = "en-US"
        }).Build();
        await middleware.InvokeAsync(context, new SessionQueries(db), new UserSettingQueries(db), TestLocales.Embedded, configuration);

        return reachedEndpoint;
    }

    [Fact]
    public async Task InvokeAsync_ContentLocaleOverridesUserSettingAndRestoresCulture()
    {
        var session = NewSession();
        var id = await SeedAsync(session);
        await using (var seed = _factory.CreateContext())
        {
            await new UserSettingCommands(seed).Set(DomainConstants.SystemUserId, UserSettingKeys.Locale, "en-US");
        }
        var context = ContextFor(ClaimsFor(id, session.AccessToken.Nonce));
        context.Request.Headers[ContentLocaleMiddleware.Header] = "hu-HU";
        var original = CultureInfo.CurrentCulture;
        var originalUi = CultureInfo.CurrentUICulture;
        SessionLogInformation? captured = null;

        await InvokeAsync(context, async _ =>
        {
            await Task.Yield();
            Assert.Equal("hu-HU", CultureInfo.CurrentCulture.Name);
            Assert.Equal("hu-HU", CultureInfo.CurrentUICulture.Name);
            Assert.Equal("1,5", 1.5m.ToString());
            captured = SessionLogContext.Current;
        });

        Assert.Equal(new SessionLogInformation("hu-HU", id, DomainConstants.SystemUserId, null), captured);
        Assert.Equal(original, CultureInfo.CurrentCulture);
        Assert.Equal(originalUi, CultureInfo.CurrentUICulture);
        Assert.Null(SessionLogContext.Current);
    }

    [Fact]
    public async Task InvokeAsync_UsesUserPreferenceThenSiteDefault()
    {
        var session = NewSession();
        var id = await SeedAsync(session);
        await using (var seed = _factory.CreateContext())
        {
            await new UserSettingCommands(seed).Set(DomainConstants.SystemUserId, UserSettingKeys.Locale, "hu-HU");
        }

        await InvokeAsync(ContextFor(ClaimsFor(id, session.AccessToken.Nonce)), _ =>
        {
            Assert.Equal("hu-HU", CultureInfo.CurrentCulture.Name);
            return Task.CompletedTask;
        });
        await InvokeAsync(new DefaultHttpContext(), _ =>
        {
            Assert.Equal("en-US", CultureInfo.CurrentCulture.Name);
            Assert.Equal(new SessionLogInformation("en-US", null, null, null), SessionLogContext.Current);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task InvokeAsync_QueryLocaleAndImpersonatorAreIncluded()
    {
        var session = NewSession();
        var id = await SeedAsync(session);
        Guid impersonator;
        await using (var seed = _factory.CreateContext())
        {
            impersonator = await DatabaseSeedHelper.SeedHumanUserAsync(seed);
            var stored = await seed.Sessions.FindAsync(id);
            seed.Entry(stored!).Property("ImpersonatorId").CurrentValue = impersonator;
            await seed.SaveChangesAsync();
        }
        var context = ContextFor(ClaimsFor(id, session.AccessToken.Nonce));
        context.Request.QueryString = new QueryString("?contentLocale=hu-HU");

        await InvokeAsync(context, _ =>
        {
            Assert.Equal(new SessionLogInformation("hu-HU", id, DomainConstants.SystemUserId, impersonator), SessionLogContext.Current);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task InvokeAsync_RestoresContextWhenEndpointThrows()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[ContentLocaleMiddleware.Header] = "hu-HU";
        var original = CultureInfo.CurrentCulture;
        var originalUi = CultureInfo.CurrentUICulture;
        using var outer = SessionLogContext.Begin(new SessionLogInformation("outer", null, null, null));

        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeAsync(context, _ =>
            throw new InvalidOperationException("test")));

        Assert.Equal(original, CultureInfo.CurrentCulture);
        Assert.Equal(originalUi, CultureInfo.CurrentUICulture);
        Assert.Equal("outer", SessionLogContext.Current?.Locale);
    }

    [Fact]
    public async Task InvokeAsync_ConcurrentRequestsKeepCulturesAndContextsSeparate()
    {
        var first = new DefaultHttpContext();
        first.Request.Headers[ContentLocaleMiddleware.Header] = "hu-HU";
        var second = new DefaultHttpContext();
        second.Request.Headers[ContentLocaleMiddleware.Header] = "en-US";
        var firstReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task Check(TaskCompletionSource ready, Task other, string expected)
        {
            ready.SetResult();
            await other;
            Assert.Equal(expected, CultureInfo.CurrentCulture.Name);
            Assert.Equal(expected, CultureInfo.CurrentUICulture.Name);
            Assert.Equal(expected, SessionLogContext.Current?.Locale);
        }

        await Task.WhenAll(
            InvokeAsync(first, _ => Check(firstReady, secondReady.Task, "hu-HU")),
            InvokeAsync(second, _ => Check(secondReady, firstReady.Task, "en-US")));
        Assert.Null(SessionLogContext.Current);
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
    public async Task InvokeAsync_AnonymousRequest_LogsDebug()
    {
        await InvokeAsync(new DefaultHttpContext());

        Assert.Equal(LogLevel.Debug, Assert.Single(_logger.Entries).Level);
    }

    [Fact]
    public async Task InvokeAsync_ValidSession_LogsDebugWithSessionId()
    {
        var session = NewSession();
        var id = await SeedAsync(session);

        await InvokeAsync(ContextFor(ClaimsFor(id, session.AccessToken.Nonce)));

        var entry = _logger.Single(LogLevel.Debug);
        Assert.Equal(id, entry.State["SessionId"]);
        Assert.False(_logger.Has(LogLevel.Warning));
    }

    [Fact]
    public async Task InvokeAsync_InvalidatedSession_LogsWarningWithSessionIdAndReason()
    {
        var session = NewSession();
        var id = await SeedAsync(session, DateTimeOffset.UtcNow);

        await InvokeAsync(ContextFor(ClaimsFor(id, session.AccessToken.Nonce)));

        var entry = _logger.Single(LogLevel.Warning);
        Assert.Equal(id, entry.State["SessionId"]);
        Assert.Equal(StatusCodes.Status401Unauthorized, entry.State["StatusCode"]);
        Assert.Equal(SessionRejection.Invalidated.Reason, entry.State["Reason"]);
    }

    [Fact]
    public async Task InvokeAsync_MissingSessionClaims_LogsWarningWithoutSessionId()
    {
        await InvokeAsync(ContextFor(new Claim(ClaimTypes.Email, "user@test.com")));

        var entry = _logger.Single(LogLevel.Warning);
        Assert.Null(entry.State["SessionId"]);
        Assert.Equal(SessionRejection.MissingSession.Reason, entry.State["Reason"]);
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
