using Behind.Core.Database.Sqlite;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase.Repository;

public sealed class SessionRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan AccessDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshDuration = TimeSpan.FromDays(7);

    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static IRepository<Session, Guid> Repo(CoreContextSqlite ctx) => ctx;

    private static ValidSession NewSession(
        string remoteAddress = "10.0.0.1",
        string userAgent = "test-agent") =>
        new(
            AccessToken.Create(new GuestUser(None), IssuedAt, AccessDuration),
            RefreshToken.Create(new GuestUser(None), IssuedAt, RefreshDuration),
            remoteAddress,
            userAgent);

    private async Task<Guid> SeedAsync(
        ValidSession session,
        Guid? userId = null,
        Guid? invalidatedById = null,
        DateTimeOffset? invalidatedAt = null)
    {
        await using var seed = _factory.CreateContext();
        return await DatabaseSeedHelper.SeedSessionAsync(
            seed,
            session,
            userId ?? DomainConstants.SystemUserId,
            invalidatedById,
            invalidatedAt);
    }

    private async Task<Session> FindAsync(Guid id)
    {
        await using var ctx = _factory.CreateContext();
        var result = await Repo(ctx).TryFindAsync(id);
        return result.Match(() => throw new Xunit.Sdk.XunitException("Expected Some"), session => session);
    }

    [Fact]
    public async Task TryFindAsync_UnknownKey_ReturnsNone()
    {
        await using var ctx = _factory.CreateContext();
        var result = await Repo(ctx).TryFindAsync(Guid.NewGuid());
        Assert.Equal(None, result);
    }

    [Fact]
    public async Task TryFindAsync_NotInvalidated_ReturnsValidSession()
    {
        var id = await SeedAsync(NewSession());
        Assert.IsType<ValidSession>(await FindAsync(id));
    }

    [Fact]
    public async Task TryFindAsync_ValidSession_RestoresAccessTokenProperties()
    {
        var original = NewSession();
        var id = await SeedAsync(original);

        var session = Assert.IsType<ValidSession>(await FindAsync(id));

        Assert.Equal(original.AccessToken.Nonce, session.AccessToken.Nonce);
        Assert.Equal(IssuedAt, session.AccessToken.IssuedAt);
        Assert.Equal(IssuedAt + AccessDuration, session.AccessToken.ExpiresAt);
    }

    [Fact]
    public async Task TryFindAsync_ValidSession_RestoresRefreshTokenProperties()
    {
        var original = NewSession();
        var id = await SeedAsync(original);

        var session = Assert.IsType<ValidSession>(await FindAsync(id));

        Assert.Equal(original.RefreshToken.Nonce, session.RefreshToken.Nonce);
        Assert.Equal(IssuedAt, session.RefreshToken.IssuedAt);
        Assert.Equal(IssuedAt + RefreshDuration, session.RefreshToken.ExpiresAt);
    }

    [Fact]
    public async Task TryFindAsync_ValidSession_RestoresRemoteAddressAndUserAgent()
    {
        var id = await SeedAsync(NewSession("192.168.1.5", "Mozilla"));

        var session = Assert.IsType<ValidSession>(await FindAsync(id));

        Assert.Equal("192.168.1.5", session.RemoteAddress.Value);
        Assert.Equal("Mozilla", session.UserAgent.Value);
    }

    [Fact]
    public async Task TryFindAsync_SystemUserId_UserIsSystemUser()
    {
        var id = await SeedAsync(NewSession(), DomainConstants.SystemUserId);

        var session = Assert.IsType<ValidSession>(await FindAsync(id));

        Assert.IsType<SystemUser>(session.User);
    }

    [Fact]
    public async Task TryFindAsync_HumanUserId_UserIsHumanUserWithEmail()
    {
        Guid userId;
        await using (var seed = _factory.CreateContext())
        {
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "session@test.com");
        }
        var id = await SeedAsync(NewSession(), userId);

        var session = Assert.IsType<ValidSession>(await FindAsync(id));

        var human = Assert.IsType<HumanUser>(session.User);
        Assert.Contains(human.Emails, e => e.Value == "session@test.com");
    }

    [Fact]
    public async Task TryFindAsync_ValidSession_TokensCarryTheSessionUser()
    {
        var id = await SeedAsync(NewSession(), DomainConstants.SystemUserId);

        var session = Assert.IsType<ValidSession>(await FindAsync(id));

        Assert.Equal(session.User, session.AccessToken.User);
        Assert.Equal(session.User, session.RefreshToken.User);
    }

    [Fact]
    public async Task TryFindAsync_InvalidatedByPopulated_ReturnsInvalidSession()
    {
        var id = await SeedAsync(NewSession(), invalidatedById: DomainConstants.SystemUserId);
        Assert.IsType<InvalidSession>(await FindAsync(id));
    }

    [Fact]
    public async Task TryFindAsync_InvalidatedAtPopulated_ReturnsInvalidSession()
    {
        var id = await SeedAsync(NewSession(), invalidatedAt: IssuedAt);
        Assert.IsType<InvalidSession>(await FindAsync(id));
    }

    [Fact]
    public async Task TryFindAsync_OnlyInvalidatedAt_InvalidatedByIsNone()
    {
        var id = await SeedAsync(NewSession(), invalidatedAt: IssuedAt);

        var session = Assert.IsType<InvalidSession>(await FindAsync(id));

        Assert.Equal(None, session.InvalidatedBy);
        Assert.Equal(Some(IssuedAt), session.InvalidatedAt);
    }

    [Fact]
    public async Task TryFindAsync_OnlyInvalidatedBy_InvalidatedAtIsNone()
    {
        var id = await SeedAsync(NewSession(), invalidatedById: DomainConstants.SystemUserId);

        var session = Assert.IsType<InvalidSession>(await FindAsync(id));

        Assert.Equal(None, session.InvalidatedAt);
        session.InvalidatedBy.Match(
            () => Assert.Fail("Expected Some"),
            user => Assert.IsType<SystemUser>(user));
    }

    [Fact]
    public async Task TryFindAsync_InvalidatedByHumanUser_ResolvesUser()
    {
        Guid adminId;
        await using (var seed = _factory.CreateContext())
        {
            adminId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "admin@test.com");
        }
        var id = await SeedAsync(NewSession(), invalidatedById: adminId, invalidatedAt: IssuedAt);

        var session = Assert.IsType<InvalidSession>(await FindAsync(id));

        session.InvalidatedBy.Match(
            () => Assert.Fail("Expected Some"),
            user => Assert.Contains(Assert.IsType<HumanUser>(user).Emails, e => e.Value == "admin@test.com"));
    }

    [Fact]
    public async Task AddAsync_ValidSession_IsPersisted()
    {
        var session = NewSession();
        Guid id;
        await using (var ctx = _factory.CreateContext())
        {
            await DatabaseSeedHelper.EnsureBuiltInUsersAsync(ctx);
            await Repo(ctx).AddAsync(session);
            ctx.Entry(session).Property("UserId").CurrentValue = DomainConstants.SystemUserId;
            id = Repo(ctx).GetKey(session);
            await ctx.SaveChangesAsync();
        }

        var found = Assert.IsType<ValidSession>(await FindAsync(id));
        Assert.Equal(session.AccessToken.Nonce, found.AccessToken.Nonce);
    }

    [Fact]
    public async Task AddAsync_InvalidSession_Throws()
    {
        await using var ctx = _factory.CreateContext();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Repo(ctx).AddAsync(new InvalidSession(None, Some(IssuedAt))));
    }

    [Fact]
    public async Task GetKey_InvalidSession_Throws()
    {
        await using var ctx = _factory.CreateContext();
        Assert.Throws<ArgumentException>(() => Repo(ctx).GetKey(new InvalidSession(None, Some(IssuedAt))));
    }

    [Fact]
    public async Task Update_InvalidSession_MarksPersistedSessionInvalid()
    {
        var id = await SeedAsync(NewSession());

        await using (var ctx = _factory.CreateContext())
        {
            await ctx.Sessions.Where(s => EF.Property<Guid>(s, "Id") == id).LoadAsync();
            Repo(ctx).Update(id, new InvalidSession(new SystemUser(None), Some(IssuedAt)));
            await ctx.SaveChangesAsync();
        }

        var session = Assert.IsType<InvalidSession>(await FindAsync(id));
        Assert.Equal(Some(IssuedAt), session.InvalidatedAt);
        session.InvalidatedBy.Match(
            () => Assert.Fail("Expected Some"),
            user => Assert.IsType<SystemUser>(user));
    }

    [Fact]
    public async Task Update_InvalidSessionInvalidatedByHumanUser_StoresUserKey()
    {
        Guid adminId;
        await using (var seed = _factory.CreateContext())
        {
            adminId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "revoker@test.com");
        }
        var id = await SeedAsync(NewSession());
        var revoker = new HumanUser("Test User", [new Email("revoker@test.com")], None);

        await using (var ctx = _factory.CreateContext())
        {
            await ctx.Sessions.Where(s => EF.Property<Guid>(s, "Id") == id).LoadAsync();
            Repo(ctx).Update(id, new InvalidSession(revoker, None));
            await ctx.SaveChangesAsync();
        }

        await using var verify = _factory.CreateContext();
        var stored = await verify.Sessions
            .Select(s => EF.Property<Guid?>(s, "InvalidatedById"))
            .SingleAsync();
        Assert.Equal(adminId, stored);
    }

    [Fact]
    public async Task Update_InvalidSessionWithoutInvalidation_Throws()
    {
        var id = await SeedAsync(NewSession());

        await using var ctx = _factory.CreateContext();
        await ctx.Sessions.Where(s => EF.Property<Guid>(s, "Id") == id).LoadAsync();

        Assert.Throws<ArgumentException>(() => Repo(ctx).Update(id, new InvalidSession(None, None)));
    }

    [Fact]
    public async Task Update_NotTracked_Throws()
    {
        var id = await SeedAsync(NewSession());

        await using var ctx = _factory.CreateContext();

        Assert.Throws<InvalidOperationException>(() =>
            Repo(ctx).Update(id, new InvalidSession(None, Some(IssuedAt))));
    }

    [Fact]
    public async Task Update_ValidSession_ReplacesTokensAndKeepsUser()
    {
        var id = await SeedAsync(NewSession(), DomainConstants.SystemUserId);
        var replacement = NewSession("172.16.0.9", "replacement-agent");

        await using (var ctx = _factory.CreateContext())
        {
            await ctx.Sessions.Where(s => EF.Property<Guid>(s, "Id") == id).LoadAsync();
            Repo(ctx).Update(id, replacement);
            await ctx.SaveChangesAsync();
        }

        var session = Assert.IsType<ValidSession>(await FindAsync(id));
        Assert.Equal(replacement.AccessToken.Nonce, session.AccessToken.Nonce);
        Assert.Equal(replacement.RefreshToken.Nonce, session.RefreshToken.Nonce);
        Assert.Equal("172.16.0.9", session.RemoteAddress.Value);
        Assert.IsType<SystemUser>(session.User);
    }

    [Fact]
    public async Task Delete_ValidSession_RemovesSession()
    {
        var id = await SeedAsync(NewSession());

        await using (var ctx = _factory.CreateContext())
        {
            var tracked = await ctx.Sessions.Where(s => EF.Property<Guid>(s, "Id") == id).FirstAsync();
            Repo(ctx).Delete(tracked);
            await ctx.SaveChangesAsync();
        }

        await using var verify = _factory.CreateContext();
        Assert.Equal(None, await Repo(verify).TryFindAsync(id));
    }

    [Fact]
    public async Task Delete_InvalidSession_Throws()
    {
        await using var ctx = _factory.CreateContext();
        Assert.Throws<ArgumentException>(() => Repo(ctx).Delete(new InvalidSession(None, Some(IssuedAt))));
    }

    [Fact]
    public async Task AddAsync_SessionsWithSameUserAgent_ShareOneUserAgentRow()
    {
        await SeedAsync(NewSession(userAgent: "shared-agent"));
        await SeedAsync(NewSession(userAgent: "shared-agent"));

        await using var verify = _factory.CreateContext();
        Assert.Equal(1, await verify.UserAgents.CountAsync());
        Assert.Equal(2, await verify.Sessions.CountAsync());
    }

    [Fact]
    public async Task AddAsync_SessionsWithDifferentUserAgents_CreateSeparateUserAgentRows()
    {
        await SeedAsync(NewSession(userAgent: "agent-one"));
        await SeedAsync(NewSession(userAgent: "agent-two"));

        await using var verify = _factory.CreateContext();
        Assert.Equal(2, await verify.UserAgents.CountAsync());
    }

    [Fact]
    public async Task AddAsync_SameNewUserAgentInOneContext_CreatesSingleUserAgentRow()
    {
        await using (var ctx = _factory.CreateContext())
        {
            await DatabaseSeedHelper.EnsureBuiltInUsersAsync(ctx);
            foreach (var session in new[] { NewSession(userAgent: "pending"), NewSession(userAgent: "pending") })
            {
                await Repo(ctx).AddAsync(session);
                ctx.Entry(session).Property("UserId").CurrentValue = DomainConstants.SystemUserId;
            }

            await ctx.SaveChangesAsync();
        }

        await using var verify = _factory.CreateContext();
        Assert.Equal(1, await verify.UserAgents.CountAsync());
    }

    [Fact]
    public async Task Update_ValidSessionWithNewUserAgent_StoresNewUserAgent()
    {
        var id = await SeedAsync(NewSession(userAgent: "old-agent"));

        await using (var ctx = _factory.CreateContext())
        {
            await ctx.Sessions.Where(s => EF.Property<Guid>(s, "Id") == id).LoadAsync();
            Repo(ctx).Update(id, NewSession(userAgent: "new-agent"));
            await ctx.SaveChangesAsync();
        }

        var session = Assert.IsType<ValidSession>(await FindAsync(id));
        Assert.Equal("new-agent", session.UserAgent.Value);
        await using var verify = _factory.CreateContext();
        Assert.Equal(2, await verify.UserAgents.CountAsync());
    }

    [Fact]
    public async Task Update_ValidSessionWithExistingUserAgent_ReusesUserAgentRow()
    {
        var id = await SeedAsync(NewSession(userAgent: "agent-a"));
        await SeedAsync(NewSession(userAgent: "agent-b"));

        await using (var ctx = _factory.CreateContext())
        {
            await ctx.Sessions.Where(s => EF.Property<Guid>(s, "Id") == id).LoadAsync();
            Repo(ctx).Update(id, NewSession(userAgent: "agent-b"));
            await ctx.SaveChangesAsync();
        }

        var session = Assert.IsType<ValidSession>(await FindAsync(id));
        Assert.Equal("agent-b", session.UserAgent.Value);
        await using var verify = _factory.CreateContext();
        Assert.Equal(2, await verify.UserAgents.CountAsync());
    }

    [Fact]
    public async Task Delete_Session_KeepsUserAgentRow()
    {
        var id = await SeedAsync(NewSession(userAgent: "kept-agent"));

        await using (var ctx = _factory.CreateContext())
        {
            var tracked = await ctx.Sessions.Where(s => EF.Property<Guid>(s, "Id") == id).FirstAsync();
            Repo(ctx).Delete(tracked);
            await ctx.SaveChangesAsync();
        }

        await using var verify = _factory.CreateContext();
        Assert.Equal(1, await verify.UserAgents.CountAsync());
    }
}
