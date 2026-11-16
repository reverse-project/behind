using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Sqlite;
using Behind.Tests.Helpers;

namespace Behind.Tests.CoreDatabase.Repository;

public sealed class UnknownUserRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<Guid> SeedDeletedUserAsync(string email)
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, email);

        await using var ctx = _factory.CreateAuditedContext(AuditActor.Anonymous);
        await ctx.GetRepository<HumanUser, Guid>().DeleteAsync(userId);
        await ctx.SaveChangesAsync();
        return userId;
    }

    [Fact]
    public async Task AuditLogEntry_ByUnresolvableUser_HasUnknownUser()
    {
        var userId = await SeedDeletedUserAsync("audit-unknown@test.com");
        await using var seed = _factory.CreateContext();
        var entryId = await DatabaseSeedHelper.SeedAuditLogEntryAsync(seed, userId, userId);

        await using var ctx = _factory.CreateContext();
        var entry = (await ctx.GetRepository<AuditLogEntry, Guid>().TryFindAsync(entryId))
            .Match(() => throw new InvalidOperationException("Expected Some"), found => found);

        Assert.Equal(new UnknownUser(userId), entry.User);
        Assert.Equal(Some<User>(new UnknownUser(userId)), entry.BehalfOf);
    }

    [Fact]
    public async Task WallOfShameEntry_ByUnresolvableUsers_HasUnknownUsers()
    {
        var userId = await SeedDeletedUserAsync("shame-unknown@test.com");
        await using var seed = _factory.CreateContext();
        var entryId = await DatabaseSeedHelper.SeedWallOfShameEntryAsync(seed, userId, userId);

        await using var ctx = _factory.CreateContext();
        var entry = (await ctx.GetRepository<WallOfShameEntry, Guid>().TryFindAsync(entryId))
            .Match(() => throw new InvalidOperationException("Expected Some"), found => found);

        Assert.Equal(new UnknownUser(userId), entry.User);
        Assert.Equal(new UnknownUser(userId), entry.GivenBy);
    }

    [Fact]
    public async Task InvalidSession_InvalidatedByUnresolvableUser_HasUnknownUser()
    {
        var userId = await SeedDeletedUserAsync("session-unknown@test.com");
        var session = new ValidSession(
            AccessToken.Create(new GuestUser(None), DateTimeOffset.UtcNow, TimeSpan.FromMinutes(15)),
            RefreshToken.Create(new GuestUser(None), DateTimeOffset.UtcNow, TimeSpan.FromDays(7)),
            "10.0.0.1",
            "agent");
        await using var seed = _factory.CreateContext();
        var sessionId = await DatabaseSeedHelper.SeedSessionAsync(
            seed,
            session,
            DomainConstants.SystemUserId,
            invalidatedById: userId,
            invalidatedAt: DateTimeOffset.UtcNow);

        await using var ctx = _factory.CreateContext();
        var found = (await ctx.GetRepository<Session, Guid>().TryFindAsync(sessionId))
            .Match(() => throw new InvalidOperationException("Expected Some"), value => value);

        var invalid = Assert.IsType<InvalidSession>(found);
        Assert.Equal(Some<User>(new UnknownUser(userId)), invalid.InvalidatedBy);
    }
}
