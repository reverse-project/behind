using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Sqlite;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase.Auditing;

public sealed class AuditSaveChangesInterceptorTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly Guid _actorId;

    public AuditSaveChangesInterceptorTests()
    {
        using var seed = _factory.CreateContext();
        _actorId = DatabaseSeedHelper.SeedHumanUserAsync(seed, "actor@test.com").GetAwaiter().GetResult();
        DatabaseSeedHelper.EnsureBuiltInUsersAsync(seed).GetAwaiter().GetResult();
    }

    public void Dispose() => _factory.Dispose();

    private CoreContextSqlite Audited(AuditActor? actor = null) =>
        _factory.CreateAuditedContext(actor ?? AuditActor.ForUser(_actorId));

    private async Task<List<AuditRow>> AuditRowsAsync(string entityName, string entityKey)
    {
        await using var ctx = _factory.CreateContext();
        return await ctx.AuditLogEntries
            .Where(a => a.EntityName == entityName && a.EntityKey == entityKey)
            .OrderBy(a => a.Timestamp)
            .Select(a => new AuditRow(a.Action, EF.Property<Guid?>(a, "UserId"), EF.Property<Guid?>(a, "BehalfOfId")))
            .ToListAsync();
    }

    private async Task<Guid> SeedEmailAsync(string value)
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, value);
        return await DatabaseSeedHelper.SeedEmailAsync(seed, userId, $"second-{value}");
    }

    private static async Task<T> LoadAsync<T>(IRepository<T, Guid> repository, Guid id) =>
        (await repository.TryFindAsync(id)).Match(() => throw new InvalidOperationException(), value => value);

    private sealed record AuditRow(AuditLogEntry.AuditAction Action, Guid? UserId, Guid? BehalfOfId);

    [Fact]
    public async Task Add_Entity_WritesCreatedAuditEntry_WithActor()
    {
        Guid id;
        await using (var ctx = Audited())
        {
            var grant = new Grant("audited-grant");
            await ctx.GetRepository<Grant, Guid>().AddAsync(grant);
            id = ctx.Entry(grant).Property<Guid>("Id").CurrentValue;
            await ctx.SaveChangesAsync();
        }

        var rows = await AuditRowsAsync(nameof(Grant), id.ToString());

        var row = Assert.Single(rows);
        Assert.IsType<AuditLogEntry.Created>(row.Action);
        Assert.Equal(_actorId, row.UserId);
        Assert.Null(row.BehalfOfId);
    }

    [Fact]
    public async Task Add_Entity_WithAnonymousActor_WritesAuditEntryWithoutUser()
    {
        Guid id;
        await using (var ctx = Audited(AuditActor.Anonymous))
        {
            var grant = new Grant("anonymous-grant");
            await ctx.GetRepository<Grant, Guid>().AddAsync(grant);
            id = ctx.Entry(grant).Property<Guid>("Id").CurrentValue;
            await ctx.SaveChangesAsync();
        }

        var row = Assert.Single(await AuditRowsAsync(nameof(Grant), id.ToString()));
        Assert.Null(row.UserId);
    }

    [Fact]
    public async Task Add_Entity_WithAutonomousActor_WritesAuditEntryForSystemUser()
    {
        Guid id;
        await using (var ctx = Audited(AuditActor.Autonomous))
        {
            var grant = new Grant("system-grant");
            await ctx.GetRepository<Grant, Guid>().AddAsync(grant);
            id = ctx.Entry(grant).Property<Guid>("Id").CurrentValue;
            await ctx.SaveChangesAsync();
        }

        var row = Assert.Single(await AuditRowsAsync(nameof(Grant), id.ToString()));
        Assert.Equal(DomainConstants.SystemUserId, row.UserId);
    }

    [Fact]
    public async Task Add_Actor_WithBehalfOf_PersistsBehalfOfId()
    {
        Guid id;
        var behalfOfId = DomainConstants.SystemUserId;
        await using (var ctx = Audited(new AuditActor(Some(_actorId), Some(behalfOfId))))
        {
            var grant = new Grant("behalf-grant");
            await ctx.GetRepository<Grant, Guid>().AddAsync(grant);
            id = ctx.Entry(grant).Property<Guid>("Id").CurrentValue;
            await ctx.SaveChangesAsync();
        }

        var row = Assert.Single(await AuditRowsAsync(nameof(Grant), id.ToString()));
        Assert.Equal(_actorId, row.UserId);
        Assert.Equal(behalfOfId, row.BehalfOfId);
    }

    [Fact]
    public async Task Add_HumanUser_IsAuditedAsUser_AndItsEmailsAsEmail()
    {
        Guid userId;
        await using (var ctx = Audited())
        {
            var user = new HumanUser("New User", [new Email("new-user@test.com")], None);
            await ctx.GetRepository<HumanUser, Guid>().AddAsync(user);
            userId = ctx.Entry(user).Property<Guid>("Id").CurrentValue;
            await ctx.SaveChangesAsync();
        }

        var userRow = Assert.Single(await AuditRowsAsync("User", userId.ToString()));
        Assert.IsType<AuditLogEntry.Created>(userRow.Action);

        await using var verify = _factory.CreateContext();
        var emailCreations = await verify.AuditLogEntries.CountAsync(a => a.EntityName == nameof(Email));
        Assert.True(emailCreations >= 1);
    }

    [Fact]
    public async Task Add_UserRole_IsAuditedWithRootEntityName()
    {
        var id = Guid.NewGuid();
        await using (var ctx = Audited())
        {
            await new RoleCommands(ctx).CreateWithId(id, new UserRole("auditor"));
        }

        var row = Assert.Single(await AuditRowsAsync("Role", id.ToString()));
        Assert.IsType<AuditLogEntry.Created>(row.Action);
    }

    [Fact]
    public async Task Update_Email_ByKey_WritesUpdatedAuditEntry_WithDifferences()
    {
        var emailId = await SeedEmailAsync("confirm-me@test.com");

        await using (var ctx = Audited())
        {
            var repository = ctx.GetRepository<Email, Guid>();
            var email = await LoadAsync(repository, emailId);
            repository.Update(emailId, email with { ConfirmedAt = DateTimeOffset.UtcNow });
            await ctx.SaveChangesAsync();
        }

        var row = Assert.Single(await AuditRowsAsync(nameof(Email), emailId.ToString()), r => r.Action is AuditLogEntry.Updated);
        var updated = Assert.IsType<AuditLogEntry.Updated>(row.Action);
        var difference = Assert.Single(updated.Differences);
        Assert.Equal(nameof(Email.ConfirmedAt), difference.PropertyName);
        Assert.Equal(_actorId, row.UserId);
    }

    [Fact]
    public async Task Update_Email_ByKey_KeepsOwningUser()
    {
        var emailId = await SeedEmailAsync("owner-kept@test.com");

        await using (var ctx = Audited())
        {
            var repository = ctx.GetRepository<Email, Guid>();
            var email = await LoadAsync(repository, emailId);
            repository.Update(emailId, email with { ConfirmedAt = DateTimeOffset.UtcNow });
            await ctx.SaveChangesAsync();
        }

        await using var verify = _factory.CreateContext();
        var userId = await verify.Emails
            .Where(e => EF.Property<Guid>(e, "Id") == emailId)
            .Select(e => EF.Property<Guid>(e, "UserId"))
            .SingleAsync();
        Assert.NotEqual(Guid.Empty, userId);
    }

    [Fact]
    public async Task Update_WithoutChanges_DoesNotWriteAuditEntry()
    {
        var emailId = await SeedEmailAsync("unchanged@test.com");

        await using (var ctx = Audited())
        {
            var repository = ctx.GetRepository<Email, Guid>();
            var email = await LoadAsync(repository, emailId);
            repository.Update(emailId, email with { });
            await ctx.SaveChangesAsync();
        }

        var rows = await AuditRowsAsync(nameof(Email), emailId.ToString());
        Assert.DoesNotContain(rows, r => r.Action is AuditLogEntry.Updated);
    }

    [Fact]
    public async Task Update_Password_MasksSensitiveValues()
    {
        Guid passwordId;
        await using (var seed = _factory.CreateContext())
        {
            var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "pw-owner@test.com");
            var password = new Password("super-secret-hash", true);
            await seed.Passwords.AddAsync(password);
            seed.Entry(password).Property<Guid>("UserId").CurrentValue = userId;
            passwordId = seed.Entry(password).Property<Guid>("Id").CurrentValue;
            await seed.SaveChangesAsync();
        }

        await using (var ctx = Audited())
        {
            var repository = ctx.GetRepository<Password, Guid>();
            var password = await LoadAsync(repository, passwordId);
            repository.Update(passwordId, password with { Value = "another-secret-hash", IsActive = false });
            await ctx.SaveChangesAsync();
        }

        var row = Assert.Single(await AuditRowsAsync(nameof(Password), passwordId.ToString()));
        var updated = Assert.IsType<AuditLogEntry.Updated>(row.Action);
        var value = Assert.Single(updated.Differences, d => d.PropertyName == nameof(Password.Value));
        Assert.Equal(EntityEntryAuditExtensions.MaskedValue, value.OldValue?.ToString());
        Assert.Equal(EntityEntryAuditExtensions.MaskedValue, value.NewValue?.ToString());
        Assert.Contains(updated.Differences, d => d.PropertyName == nameof(Password.IsActive));
    }

    [Fact]
    public async Task Update_Session_AuditsInvalidation()
    {
        var sessionId = await SeedSessionAsync();

        await using (var ctx = Audited())
        {
            ctx.GetRepository<Session, Guid>()
                .Update(sessionId, new InvalidSession(Some<User>(new SystemUser(None)), None));
            await ctx.SaveChangesAsync();
        }

        var rows = await AuditRowsAsync("ValidSession", sessionId.ToString());
        var updated = Assert.IsType<AuditLogEntry.Updated>(Assert.Single(rows, r => r.Action is AuditLogEntry.Updated).Action);
        Assert.Contains(updated.Differences, d => d.PropertyName == "InvalidatedById");
    }

    [Fact]
    public async Task Replace_Session_MasksTokenNonces()
    {
        var sessionId = await SeedSessionAsync();

        await using (var ctx = Audited())
        {
            var replacement = NewSession();
            ctx.GetRepository<Session, Guid>().Update(sessionId, replacement);
            await ctx.SaveChangesAsync();
        }

        var rows = await AuditRowsAsync("ValidSession", sessionId.ToString());
        var updated = Assert.IsType<AuditLogEntry.Updated>(Assert.Single(rows, r => r.Action is AuditLogEntry.Updated).Action);
        var nonces = updated.Differences.Where(d => d.PropertyName is "AccessToken.Nonce" or "RefreshToken.Nonce").ToList();
        Assert.Equal(2, nonces.Count);
        Assert.All(nonces, d => Assert.Equal(EntityEntryAuditExtensions.MaskedValue, d.NewValue?.ToString()));
    }

    [Fact]
    public async Task Delete_ByKey_RemovesRow_AndWritesDeletedAuditEntry()
    {
        var emailId = await SeedEmailAsync("delete-me@test.com");

        await using (var ctx = Audited())
        {
            await ctx.GetRepository<Email, Guid>().DeleteAsync(emailId);
            await ctx.SaveChangesAsync();
        }

        var rows = await AuditRowsAsync(nameof(Email), emailId.ToString());
        Assert.Contains(rows, r => r.Action is AuditLogEntry.Deleted && r.UserId == _actorId);

        await using var verify = _factory.CreateContext();
        Assert.False(await verify.Emails.AnyAsync(e => EF.Property<Guid>(e, "Id") == emailId));
    }

    [Fact]
    public async Task Delete_HumanUser_ByKey_RemovesUserWithItsEmails()
    {
        Guid userId;
        await using (var seed = _factory.CreateContext())
        {
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "gone@test.com");
        }

        await using (var ctx = Audited())
        {
            await ctx.GetRepository<HumanUser, Guid>().DeleteAsync(userId);
            await ctx.SaveChangesAsync();
        }

        Assert.Contains(await AuditRowsAsync("User", userId.ToString()), r => r.Action is AuditLogEntry.Deleted);

        await using var verify = _factory.CreateContext();
        Assert.False(await verify.Users.AnyAsync(u => EF.Property<Guid>(u, "Id") == userId));
        Assert.False(await verify.Emails.AnyAsync(e => EF.Property<Guid>(e, "UserId") == userId));
    }

    [Fact]
    public async Task Delete_UnknownKey_DoesNothing()
    {
        await using var ctx = Audited();
        await ctx.GetRepository<Grant, Guid>().DeleteAsync(Guid.NewGuid());

        Assert.Equal(0, await ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Create_Update_Delete_InSingleSave_AreAuditedAtomically()
    {
        Guid id;
        await using (var ctx = Audited())
        {
            var grant = new Grant("atomic");
            await ctx.GetRepository<Grant, Guid>().AddAsync(grant);
            id = ctx.Entry(grant).Property<Guid>("Id").CurrentValue;
            await ctx.SaveChangesAsync();

            ctx.GetRepository<Grant, Guid>().Update(id, new Grant("atomic-renamed"));
            await ctx.SaveChangesAsync();

            await ctx.GetRepository<Grant, Guid>().DeleteAsync(id);
            await ctx.SaveChangesAsync();
        }

        var rows = await AuditRowsAsync(nameof(Grant), id.ToString());
        Assert.Collection(
            rows,
            r => Assert.IsType<AuditLogEntry.Created>(r.Action),
            r => Assert.IsType<AuditLogEntry.Updated>(r.Action),
            r => Assert.IsType<AuditLogEntry.Deleted>(r.Action));
    }

    [Fact]
    public async Task AuditLogEntries_AreNotAudited()
    {
        await using (var ctx = Audited())
        {
            await ctx.GetRepository<AuditLogEntry, Guid>()
                .AddAsync(new AuditLogEntry("Manual", "1", new AuditLogEntry.Created(), DateTimeOffset.UtcNow));
            await ctx.SaveChangesAsync();
        }

        await using var verify = _factory.CreateContext();
        Assert.False(await verify.AuditLogEntries.AnyAsync(a => a.EntityName == nameof(AuditLogEntry)));
    }

    [Fact]
    public async Task Unaudited_Context_DoesNotWriteAuditEntries()
    {
        Guid id;
        await using (var ctx = _factory.CreateContext())
        {
            var grant = new Grant("unaudited");
            await ctx.GetRepository<Grant, Guid>().AddAsync(grant);
            id = ctx.Entry(grant).Property<Guid>("Id").CurrentValue;
            await ctx.SaveChangesAsync();
        }

        Assert.Empty(await AuditRowsAsync(nameof(Grant), id.ToString()));
    }

    [Fact]
    public async Task SaveChanges_Synchronously_IsNotSupported()
    {
        await using var ctx = Audited();
        await ctx.GetRepository<Grant, Guid>().AddAsync(new Grant("sync"));

        Assert.Throws<NotSupportedException>(() => ctx.SaveChanges());
    }

    [Fact]
    public async Task AutonomousProvider_ReturnsSystemActor()
    {
        var actor = await new AutonomousAuditActorProvider().GetCurrentAsync();

        Assert.Equal(AuditActor.Autonomous, actor);
        Assert.Equal(Some(DomainConstants.SystemUserId), actor.UserId);
        Assert.Equal(None, actor.BehalfOfId);
    }

    [Fact]
    public void AnonymousActor_HasNoUser()
    {
        Assert.Equal(None, AuditActor.Anonymous.UserId);
    }

    [Fact]
    public async Task EnsureBuiltInUsers_IsIdempotent()
    {
        await using var ctx = _factory.CreateContext();

        await ctx.EnsureBuiltInUsersAsync();
        await ctx.EnsureBuiltInUsersAsync();

        Assert.Equal(1, await ctx.Users.CountAsync(u => EF.Property<Guid>(u, "Id") == DomainConstants.SystemUserId));
        Assert.Equal(1, await ctx.Users.CountAsync(u => EF.Property<Guid>(u, "Id") == DomainConstants.GuestUserId));
    }

    private static ValidSession NewSession() =>
        new(
            AccessToken.Create(new GuestUser(None)),
            RefreshToken.Create(new GuestUser(None)),
            "10.0.0.1",
            "audit-agent");

    private async Task<Guid> SeedSessionAsync()
    {
        await using var seed = _factory.CreateContext();
        return await DatabaseSeedHelper.SeedSessionAsync(seed, NewSession(), _actorId);
    }
}
