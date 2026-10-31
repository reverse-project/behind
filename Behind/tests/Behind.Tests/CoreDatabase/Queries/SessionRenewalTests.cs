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
    public async Task RenewAccessToken_MatchingNonce_ReplacesAccessTokenAndKeepsRefreshToken()
    {
        var (sessionId, session) = await SeedSessionAsync("renew-ok@test.com");
        var renewed = AccessToken.Create(new GuestUser(None), TimeSpan.FromMinutes(45));

        await using (var ctx = _factory.CreateContext())
        {
            Assert.True(await new SessionCommands(ctx).RenewAccessToken(
                sessionId,
                session.AccessToken.Nonce,
                renewed));
        }

        var reloaded = await ReloadAsync(sessionId);
        Assert.Equal(renewed.Nonce, reloaded.AccessToken.Nonce);
        Assert.Equal(renewed.ExpiresAt.ToUnixTimeSeconds(), reloaded.AccessToken.ExpiresAt.ToUnixTimeSeconds());
        Assert.Equal(session.RefreshToken.Nonce, reloaded.RefreshToken.Nonce);
    }

    [Fact]
    public async Task RenewAccessToken_StaleNonce_ReturnsFalseAndKeepsAccessToken()
    {
        var (sessionId, session) = await SeedSessionAsync("renew-stale@test.com");

        await using (var ctx = _factory.CreateContext())
        {
            Assert.False(await new SessionCommands(ctx).RenewAccessToken(
                sessionId,
                Guid.NewGuid(),
                AccessToken.Create(new GuestUser(None))));
        }

        Assert.Equal(session.AccessToken.Nonce, (await ReloadAsync(sessionId)).AccessToken.Nonce);
    }

    [Fact]
    public async Task RenewAccessToken_InvalidatedSession_ReturnsFalse()
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
        Assert.False(await new SessionCommands(renewing).RenewAccessToken(
            sessionId,
            session.AccessToken.Nonce,
            AccessToken.Create(new GuestUser(None))));
    }

    [Fact]
    public async Task RenewAccessToken_UnknownSession_ReturnsFalse()
    {
        await using var ctx = _factory.CreateContext();

        Assert.False(await new SessionCommands(ctx).RenewAccessToken(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AccessToken.Create(new GuestUser(None))));
    }
}
