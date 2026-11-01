using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Tests.Helpers;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class SessionRenewalTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static ValidSession NewSession() =>
        new(
            AccessToken.Create(new GuestUser(None)),
            RefreshToken.Create(new GuestUser(None)),
            "10.0.0.1",
            "test-agent");

    private async Task<(Guid SessionId, ValidSession Session)> SeedSessionAsync(string email)
    {
        var session = NewSession();
        await using var ctx = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(ctx, email);
        var sessionId = await new SessionCommands(ctx).CreateForUserId(userId, session);
        return (sessionId, session);
    }

    private async Task<ValidSession> ReloadAsync(Guid sessionId)
    {
        await using var ctx = _factory.CreateContext();
        var found = await new SessionQueries(ctx).TryFindById(sessionId);
        return Assert.IsType<ValidSession>(found.Match(() => throw new Xunit.Sdk.XunitException("None"), s => s));
    }

    [Fact]
    public async Task RenewTokens_MatchingNonces_ReplacesBothTokensAndKeepsTheRefreshLifetime()
    {
        var (sessionId, session) = await SeedSessionAsync("renew-ok@test.com");
        var renewed = AccessToken.Create(new GuestUser(None), TimeSpan.FromMinutes(45));
        var rotated = session.RefreshToken.Rotate();

        await using (var ctx = _factory.CreateContext())
        {
            Assert.True(await new SessionCommands(ctx).RenewTokens(
                sessionId,
                session.AccessToken.Nonce,
                session.RefreshToken.Nonce,
                renewed,
                rotated));
        }

        var reloaded = await ReloadAsync(sessionId);
        Assert.Equal(renewed.Nonce, reloaded.AccessToken.Nonce);
        Assert.Equal(renewed.ExpiresAt.ToUnixTimeSeconds(), reloaded.AccessToken.ExpiresAt.ToUnixTimeSeconds());
        Assert.Equal(rotated.Nonce, reloaded.RefreshToken.Nonce);
        Assert.NotEqual(session.RefreshToken.Nonce, reloaded.RefreshToken.Nonce);
        Assert.Equal(session.RefreshToken.IssuedAt.ToUnixTimeSeconds(), reloaded.RefreshToken.IssuedAt.ToUnixTimeSeconds());
        Assert.Equal(session.RefreshToken.ExpiresAt.ToUnixTimeSeconds(), reloaded.RefreshToken.ExpiresAt.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task RenewTokens_StaleAccessNonce_ReturnsFalseAndKeepsTokens()
    {
        var (sessionId, session) = await SeedSessionAsync("renew-stale@test.com");

        await using (var ctx = _factory.CreateContext())
        {
            Assert.False(await new SessionCommands(ctx).RenewTokens(
                sessionId,
                Guid.NewGuid(),
                session.RefreshToken.Nonce,
                AccessToken.Create(new GuestUser(None)),
                session.RefreshToken.Rotate()));
        }

        var reloaded = await ReloadAsync(sessionId);
        Assert.Equal(session.AccessToken.Nonce, reloaded.AccessToken.Nonce);
        Assert.Equal(session.RefreshToken.Nonce, reloaded.RefreshToken.Nonce);
    }

    [Fact]
    public async Task RenewTokens_AlreadyRotatedRefreshNonce_ReturnsFalseAndKeepsTokens()
    {
        var (sessionId, session) = await SeedSessionAsync("renew-replayed@test.com");

        await using (var ctx = _factory.CreateContext())
        {
            Assert.True(await new SessionCommands(ctx).RenewTokens(
                sessionId,
                session.AccessToken.Nonce,
                session.RefreshToken.Nonce,
                AccessToken.Create(new GuestUser(None)),
                session.RefreshToken.Rotate()));
        }

        var afterFirst = await ReloadAsync(sessionId);

        await using (var ctx = _factory.CreateContext())
        {
            Assert.False(await new SessionCommands(ctx).RenewTokens(
                sessionId,
                afterFirst.AccessToken.Nonce,
                session.RefreshToken.Nonce,
                AccessToken.Create(new GuestUser(None)),
                session.RefreshToken.Rotate()));
        }

        var reloaded = await ReloadAsync(sessionId);
        Assert.Equal(afterFirst.AccessToken.Nonce, reloaded.AccessToken.Nonce);
        Assert.Equal(afterFirst.RefreshToken.Nonce, reloaded.RefreshToken.Nonce);
    }

    [Fact]
    public async Task RenewTokens_InvalidatedSession_ReturnsFalse()
    {
        var (sessionId, session) = await SeedSessionAsync("renew-invalid@test.com");

        await using (var ctx = _factory.CreateContext())
        {
            await DatabaseSeedHelper.EnsureBuiltInUsersAsync(ctx);
            await new SessionCommands(ctx).InvalidateSession(
                sessionId,
                DomainConstants.SystemUserId,
                DateTimeOffset.UtcNow);
        }

        await using var renewing = _factory.CreateContext();
        Assert.False(await new SessionCommands(renewing).RenewTokens(
            sessionId,
            session.AccessToken.Nonce,
            session.RefreshToken.Nonce,
            AccessToken.Create(new GuestUser(None)),
            session.RefreshToken.Rotate()));
    }

    [Fact]
    public async Task RenewTokens_UnknownSession_ReturnsFalse()
    {
        await using var ctx = _factory.CreateContext();

        Assert.False(await new SessionCommands(ctx).RenewTokens(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            AccessToken.Create(new GuestUser(None)),
            RefreshToken.Create(new GuestUser(None))));
    }
}
