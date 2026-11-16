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
        {
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "commands@test.com");
        }

        var session = NewSession();
        Guid sessionId;
        await using (var ctx = _factory.CreateContext())
        {
            sessionId = await new SessionCommands(ctx).CreateForUserId(userId, session);
        }

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

    [Fact]
    public async Task InvalidateSession_WithValidSession_PopulatesInvalidationFields()
    {
        Guid userId;
        await using (var seed = _factory.CreateContext())
        {
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "invalidate@test.com");
        }

        var session = NewSession();
        Guid sessionId;
        await using (var ctx = _factory.CreateContext())
        {
            sessionId = await new SessionCommands(ctx).CreateForUserId(userId, session);
        }

        var invalidatedAt = TimeProvider.System.GetUtcNow();
        await using (var ctx = _factory.CreateContext())
        {
            await new SessionCommands(ctx).InvalidateSession(sessionId, userId, invalidatedAt);
        }

        await using var reader = _factory.CreateContext();
        var found = await new SessionQueries(reader).TryFindById(sessionId);

        var invalid = Assert.IsType<InvalidSession>(found.Match(() => throw new Xunit.Sdk.XunitException("None"), s => s));
        var invalidatedBy = invalid.InvalidatedBy.Match(() => throw new Xunit.Sdk.XunitException("None"), u => u);
        Assert.IsType<HumanUser>(invalidatedBy);
        Assert.Equal(invalidatedAt, invalid.InvalidatedAt.Match(() => throw new Xunit.Sdk.XunitException("None"), at => at));
    }

    [Fact]
    public async Task InvalidateAllSessions_WithMultipleSessions_InvalidatesAll()
    {
        Guid userId;
        await using (var seed = _factory.CreateContext())
        {
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "invalidate-all@test.com");
        }

        var sessionIds = new List<Guid>();
        for (int i = 0; i < 3; i++)
        {
            var session = NewSession();
            await using (var ctx = _factory.CreateContext())
            {
                sessionIds.Add(await new SessionCommands(ctx).CreateForUserId(userId, session));
            }
        }

        var invalidatedAt = TimeProvider.System.GetUtcNow();
        await using (var ctx = _factory.CreateContext())
        {
            await new SessionCommands(ctx).InvalidateAllSessions(userId, userId, invalidatedAt);
        }

        await using var reader = _factory.CreateContext();
        foreach (var sessionId in sessionIds)
        {
            var found = await new SessionQueries(reader).TryFindById(sessionId);
            var invalid = Assert.IsType<InvalidSession>(found.Match(() => throw new Xunit.Sdk.XunitException("None"), s => s));
            Assert.True(invalid.InvalidatedBy != None);
            Assert.True(invalid.InvalidatedAt != None);
        }
    }

    [Fact]
    public async Task InvalidateAllSessions_OnlyInvalidatesSpecificUser()
    {
        Guid userId1, userId2;
        await using (var seed = _factory.CreateContext())
        {
            userId1 = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "user1@test.com");
            userId2 = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "user2@test.com");
        }

        Guid sessionId1, sessionId2;
        await using (var ctx = _factory.CreateContext())
        {
            sessionId1 = await new SessionCommands(ctx).CreateForUserId(userId1, NewSession());
            sessionId2 = await new SessionCommands(ctx).CreateForUserId(userId2, NewSession());
        }

        var invalidatedAt = TimeProvider.System.GetUtcNow();
        await using (var ctx = _factory.CreateContext())
        {
            await new SessionCommands(ctx).InvalidateAllSessions(userId1, userId1, invalidatedAt);
        }

        await using var reader = _factory.CreateContext();
        var session1 = await new SessionQueries(reader).TryFindById(sessionId1);
        var session2 = await new SessionQueries(reader).TryFindById(sessionId2);

        Assert.IsType<InvalidSession>(session1.Match(() => throw new Xunit.Sdk.XunitException("None"), s => s));
        Assert.IsType<ValidSession>(session2.Match(() => throw new Xunit.Sdk.XunitException("None"), s => s));
    }

    [Fact]
    public async Task InvalidateSession_AlreadyInvalidated_KeepsOriginalInvalidation()
    {
        Guid userId;
        await using (var seed = _factory.CreateContext())
        {
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "twice@test.com");
        }

        Guid sessionId;
        await using (var ctx = _factory.CreateContext())
        {
            sessionId = await new SessionCommands(ctx).CreateForUserId(userId, NewSession());
        }

        var firstInvalidation = TimeProvider.System.GetUtcNow();
        await using (var ctx = _factory.CreateContext())
        {
            await new SessionCommands(ctx).InvalidateSession(sessionId, userId, firstInvalidation);
        }

        await using (var ctx = _factory.CreateContext())
        {
            await new SessionCommands(ctx).InvalidateSession(sessionId, userId, firstInvalidation.AddHours(1));
        }

        await using var reader = _factory.CreateContext();
        var found = await new SessionQueries(reader).TryFindById(sessionId);

        var invalid = Assert.IsType<InvalidSession>(found.Match(() => throw new Xunit.Sdk.XunitException("None"), s => s));
        Assert.Equal(firstInvalidation, invalid.InvalidatedAt.Match(() => throw new Xunit.Sdk.XunitException("None"), at => at));
    }

    [Fact]
    public async Task InvalidateAllSessions_SkipsAlreadyInvalidatedSessions()
    {
        Guid userId;
        await using (var seed = _factory.CreateContext())
        {
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "skip@test.com");
        }

        Guid firstSessionId;
        Guid secondSessionId;
        await using (var ctx = _factory.CreateContext())
        {
            firstSessionId = await new SessionCommands(ctx).CreateForUserId(userId, NewSession());
            secondSessionId = await new SessionCommands(ctx).CreateForUserId(userId, NewSession());
        }

        var firstInvalidation = TimeProvider.System.GetUtcNow();
        await using (var ctx = _factory.CreateContext())
        {
            await new SessionCommands(ctx).InvalidateSession(firstSessionId, userId, firstInvalidation);
        }

        await using (var ctx = _factory.CreateContext())
        {
            await new SessionCommands(ctx).InvalidateAllSessions(userId, userId, firstInvalidation.AddHours(1));
        }

        await using var reader = _factory.CreateContext();
        var first = await new SessionQueries(reader).TryFindById(firstSessionId);
        var second = await new SessionQueries(reader).TryFindById(secondSessionId);

        var firstInvalid = Assert.IsType<InvalidSession>(first.Match(() => throw new Xunit.Sdk.XunitException("None"), s => s));
        var secondInvalid = Assert.IsType<InvalidSession>(second.Match(() => throw new Xunit.Sdk.XunitException("None"), s => s));
        Assert.Equal(firstInvalidation, firstInvalid.InvalidatedAt.Match(() => throw new Xunit.Sdk.XunitException("None"), at => at));
        Assert.Equal(firstInvalidation.AddHours(1), secondInvalid.InvalidatedAt.Match(() => throw new Xunit.Sdk.XunitException("None"), at => at));
    }
}

