using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Tests.Helpers;

namespace Behind.Tests.CoreDatabase.Repository;

public sealed class SessionCommandsTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static ValidSession NewSession() =>
        new(
            AccessToken.Create(new GuestUser(None)),
            RefreshToken.Create(new GuestUser(None)),
            "10.0.0.1",
            "test-agent");

    [Fact]
    public async Task CreateForUserId_PersistsSessionForUser()
    {
        Guid userId;
        await using (var seed = _factory.CreateContext())
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "commands@test.com");

        var session = NewSession();
        Guid sessionId;
        await using (var ctx = _factory.CreateContext())
            sessionId = await new SessionCommands(ctx).CreateForUserId(userId, session);

        await using var reader = _factory.CreateContext();
        var found = await new SessionQueries(reader).TryFindById(sessionId);

        var stored = Assert.IsType<ValidSession>(found.Match(() => throw new Xunit.Sdk.XunitException("None"), s => s));
        Assert.Equal(session.AccessToken.Nonce, stored.AccessToken.Nonce);
        Assert.Equal(session.RefreshToken.Nonce, stored.RefreshToken.Nonce);
        Assert.IsType<HumanUser>(stored.User);
    }

    [Fact]
    public async Task TryFindById_UnknownId_ReturnsNone()
    {
        await using var ctx = _factory.CreateContext();

        Assert.Equal(None, await new SessionQueries(ctx).TryFindById(Guid.NewGuid()));
    }
}
