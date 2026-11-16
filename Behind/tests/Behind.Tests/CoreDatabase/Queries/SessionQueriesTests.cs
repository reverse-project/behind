using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Tests.Helpers;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class SessionQueriesTests : IDisposable
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
    public async Task TryFindByIdWithUserId_ExistingSession_ReturnsSessionAndOwner()
    {
        Guid userId;
        await using (var seed = _factory.CreateContext())
        {
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "owner-query@test.com");
        }

        Guid sessionId;
        await using (var ctx = _factory.CreateContext())
        {
            sessionId = await new SessionCommands(ctx).CreateForUserId(userId, NewSession());
        }

        await using var reader = _factory.CreateContext();
        var found = await new SessionQueries(reader).TryFindByIdWithUserId(sessionId);

        var (session, owner) = found.Match(() => throw new Xunit.Sdk.XunitException("None"), result => result);
        Assert.IsType<ValidSession>(session);
        Assert.Equal(userId, owner);
    }

    [Fact]
    public async Task TryFindByIdWithUserId_UnknownSession_ReturnsNone()
    {
        await using var ctx = _factory.CreateContext();

        Assert.Equal(None, await new SessionQueries(ctx).TryFindByIdWithUserId(Guid.NewGuid()));
    }
}
