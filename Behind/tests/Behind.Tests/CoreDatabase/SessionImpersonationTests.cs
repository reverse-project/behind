using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class SessionImpersonationTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly Guid _impersonatorId;
    private readonly Guid _targetId;

    public SessionImpersonationTests()
    {
        using var seed = _factory.CreateContext();
        DatabaseSeedHelper.EnsureBuiltInUsersAsync(seed).GetAwaiter().GetResult();
        _impersonatorId = DatabaseSeedHelper.SeedHumanUserAsync(seed, "admin@test.com").GetAwaiter().GetResult();
        _targetId = DatabaseSeedHelper.SeedHumanUserAsync(seed, "target@test.com").GetAwaiter().GetResult();
    }

    public void Dispose() => _factory.Dispose();

    private static ValidSession NewSession() =>
        new(
            AccessToken.Create(new GuestUser(None)),
            RefreshToken.Create(new GuestUser(None)),
            "10.0.0.1",
            "test-agent");

    private async Task<Guid> StartImpersonationAsync()
    {
        await using var ctx = _factory.CreateAuditedContext(AuditActor.ForUser(_impersonatorId));

        return await new SessionCommands(ctx).CreateImpersonation(_impersonatorId, _targetId, NewSession());
    }

    private async Task<List<AuditLogEntry>> AuditLogAsync()
    {
        await using var ctx = _factory.CreateContext();
        var request = new IAuditLogEntryQueries.RequestAuditLog(
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            new EmptyFilter<AuditLogEntry>(),
            new Flipper(1, 100),
            new OrderByCollection());

        var page = await new AuditLogEntryQueries(ctx, []).GetAuditLog(request);

        return page
            .Bind(paged => paged.Results)
            .Match(() => [], items => items.ToList());
    }

    [Fact]
    public async Task CreateImpersonation_StoresTheImpersonatorOnTheSession()
    {
        var sessionId = await StartImpersonationAsync();

        await using var ctx = _factory.CreateContext();
        var actor = await new SessionQueries(ctx).TryFindActor(sessionId);

        var found = actor.Match(() => throw new Xunit.Sdk.XunitException("None"), value => value);
        Assert.Equal(_targetId, found.UserId);
        Assert.Equal(Some(_impersonatorId), found.ImpersonatorId);
    }

    [Fact]
    public async Task CreateForUserId_HasNoImpersonator()
    {
        Guid sessionId;
        await using (var ctx = _factory.CreateContext())
        {
            sessionId = await new SessionCommands(ctx).CreateForUserId(_targetId, NewSession());
        }

        await using var reader = _factory.CreateContext();
        var actor = await new SessionQueries(reader).TryFindActor(sessionId);

        var found = actor.Match(() => throw new Xunit.Sdk.XunitException("None"), value => value);
        Assert.Equal(_targetId, found.UserId);
        Assert.Equal(None, found.ImpersonatorId);
    }

    [Fact]
    public async Task TryFindActor_UnknownSession_ReturnsNone()
    {
        await using var ctx = _factory.CreateContext();

        Assert.Equal(None, await new SessionQueries(ctx).TryFindActor(Guid.NewGuid()));
    }

    [Fact]
    public async Task TryFindById_ImpersonationSession_ResolvesBothUsers()
    {
        var sessionId = await StartImpersonationAsync();

        await using var ctx = _factory.CreateContext();
        var found = await new SessionQueries(ctx).TryFindById(sessionId);

        var session = Assert.IsType<ValidSession>(
            found.Match(() => throw new Xunit.Sdk.XunitException("None"), value => value));
        var user = Assert.IsType<HumanUser>(session.User);
        var impersonator = Assert.IsType<HumanUser>(
            session.Impersonator.Match(() => throw new Xunit.Sdk.XunitException("None"), value => value));
        Assert.Equal("target@test.com", user.Emails.Single().Value);
        Assert.Equal("admin@test.com", impersonator.Emails.Single().Value);
    }

    [Fact]
    public async Task TryFindById_PlainSession_HasNoImpersonator()
    {
        Guid sessionId;
        await using (var ctx = _factory.CreateContext())
        {
            sessionId = await new SessionCommands(ctx).CreateForUserId(_targetId, NewSession());
        }

        await using var reader = _factory.CreateContext();
        var found = await new SessionQueries(reader).TryFindById(sessionId);

        var session = Assert.IsType<ValidSession>(
            found.Match(() => throw new Xunit.Sdk.XunitException("None"), value => value));
        Assert.Equal(None, session.Impersonator);
    }

    [Fact]
    public async Task RenewTokens_KeepsTheImpersonator()
    {
        var sessionId = await StartImpersonationAsync();
        ValidSession original;
        await using (var ctx = _factory.CreateContext())
        {
            var found = await new SessionQueries(ctx).TryFindById(sessionId);
            original = (ValidSession)found.Match(() => throw new Xunit.Sdk.XunitException("None"), value => value);
        }

        await using (var ctx = _factory.CreateContext())
        {
            Assert.True(await new SessionCommands(ctx).RenewTokens(
                sessionId,
                original.AccessToken.Nonce,
                original.RefreshToken.Nonce,
                AccessToken.Create(new GuestUser(None)),
                original.RefreshToken.Rotate()));
        }

        await using var reader = _factory.CreateContext();
        var actor = await new SessionQueries(reader).TryFindActor(sessionId);
        Assert.Equal(
            Some(_impersonatorId),
            actor.Match(() => throw new Xunit.Sdk.XunitException("None"), value => value).ImpersonatorId);
    }

    [Fact]
    public async Task StartingImpersonation_IsAuditedAsImpersonationStartedByTheImpersonatorOnBehalfOfTheTarget()
    {
        var sessionId = await StartImpersonationAsync();

        var entry = Assert.Single(
            await AuditLogAsync(),
            candidate => candidate.EntityKey == sessionId.ToString());
        Assert.IsType<AuditLogEntry.ImpersonationStarted>(entry.Action);
        Assert.Equal(nameof(ValidSession), entry.EntityName);
        Assert.Equal("admin@test.com", Assert.IsType<HumanUser>(entry.User).Emails.Single().Value);
        var behalfOf = entry.BehalfOf.Match(() => throw new Xunit.Sdk.XunitException("None"), value => value);
        Assert.Equal("target@test.com", Assert.IsType<HumanUser>(behalfOf).Emails.Single().Value);
    }

    [Fact]
    public async Task StartingAPlainSession_IsStillAuditedAsCreated()
    {
        Guid sessionId;
        await using (var ctx = _factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            sessionId = await new SessionCommands(ctx).CreateForUserId(_targetId, NewSession());
        }

        var entry = Assert.Single(
            await AuditLogAsync(),
            candidate => candidate.EntityKey == sessionId.ToString());
        Assert.IsType<AuditLogEntry.Created>(entry.Action);
    }

    [Fact]
    public async Task StoppingImpersonation_IsAuditedAsImpersonationEnded()
    {
        var sessionId = await StartImpersonationAsync();

        await using (var ctx = _factory.CreateAuditedContext(AuditActor.ForSession(_targetId, Some(_impersonatorId))))
        {
            await new SessionCommands(ctx).InvalidateSession(sessionId, _impersonatorId, DateTimeOffset.UtcNow);
        }

        var entries = (await AuditLogAsync())
            .Where(candidate => candidate.EntityKey == sessionId.ToString())
            .ToList();
        var ended = Assert.Single(entries, candidate => candidate.Action is AuditLogEntry.ImpersonationEnded);
        Assert.Equal("admin@test.com", Assert.IsType<HumanUser>(ended.User).Emails.Single().Value);
        Assert.Equal(
            "target@test.com",
            Assert.IsType<HumanUser>(
                ended.BehalfOf.Match(() => throw new Xunit.Sdk.XunitException("None"), value => value))
                .Emails.Single().Value);
    }

    [Fact]
    public async Task InvalidatingAPlainSession_IsAuditedAsUpdated()
    {
        Guid sessionId;
        await using (var ctx = _factory.CreateContext())
        {
            sessionId = await new SessionCommands(ctx).CreateForUserId(_targetId, NewSession());
        }

        await using (var ctx = _factory.CreateAuditedContext(AuditActor.ForUser(_targetId)))
        {
            await new SessionCommands(ctx).InvalidateSession(sessionId, _targetId, DateTimeOffset.UtcNow);
        }

        var entry = Assert.Single(
            await AuditLogAsync(),
            candidate => candidate.EntityKey == sessionId.ToString());
        Assert.IsType<AuditLogEntry.Updated>(entry.Action);
    }

    [Fact]
    public async Task ChangesMadeWhileImpersonating_AreAuditedWithTheImpersonatorOnBehalfOfTheTarget()
    {
        await using (var ctx = _factory.CreateAuditedContext(AuditActor.ForSession(_targetId, Some(_impersonatorId))))
        {
            await new UserCommands(ctx).ChangeDisplayName(_targetId, "Changed".ToDisplayName());
        }

        var entry = Assert.Single(
            await AuditLogAsync(),
            candidate => candidate.Action is AuditLogEntry.Updated && candidate.EntityName == nameof(User));
        Assert.Equal("admin@test.com", Assert.IsType<HumanUser>(entry.User).Emails.Single().Value);
        Assert.Equal(
            "target@test.com",
            Assert.IsType<HumanUser>(
                entry.BehalfOf.Match(() => throw new Xunit.Sdk.XunitException("None"), value => value))
                .Emails.Single().Value);
    }

    [Fact]
    public void ForSession_WithoutImpersonator_IsTheUser()
    {
        var userId = Guid.NewGuid();

        Assert.Equal(AuditActor.ForUser(userId), AuditActor.ForSession(userId, None));
    }

    [Fact]
    public void ForSession_WithImpersonator_IsTheImpersonatorOnBehalfOfTheUser()
    {
        var userId = Guid.NewGuid();
        var impersonatorId = Guid.NewGuid();

        var actor = AuditActor.ForSession(userId, Some(impersonatorId));

        Assert.Equal(Some(impersonatorId), actor.UserId);
        Assert.Equal(Some(userId), actor.BehalfOfId);
    }
}
