using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.Database.Sqlite;
using Behind.Core.WebApi.Identity;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreWebApi.Identity;

public sealed class IdentityStoreAuditTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly Guid _actorId;

    public IdentityStoreAuditTests()
    {
        using var seed = _factory.CreateContext();
        _actorId = DatabaseSeedHelper.SeedHumanUserAsync(seed, "store-actor@test.com").GetAwaiter().GetResult();
    }

    public void Dispose() => _factory.Dispose();

    private static IdentityStore StoreFor(CoreContextSqlite context) =>
        new(
            context,
            new UserQueries(context),
            new EmailQueries(context),
            new EmailCommands(context, TimeProvider.System),
            new PasswordQueries(context),
            new RoleQueries(context),
            new PasswordCommands(context),
            new UserCommands(context),
            new RoleCommands(context),
            NullLogger<IdentityStore>.Instance);

    private CoreContextSqlite Audited() => _factory.CreateAuditedContext(AuditActor.ForUser(_actorId));

    private async Task<List<AuditLogEntry.AuditAction>> ActionsAsync(string entityName, Guid key)
    {
        await using var ctx = _factory.CreateContext();
        return await ctx.AuditLogEntries
            .Where(a => a.EntityName == entityName && a.EntityKey == key.ToString())
            .OrderBy(a => a.Timestamp)
            .Select(a => a.Action)
            .ToListAsync();
    }

    private static ApplicationUser NewUser(string email) =>
        new() { DisplayName = "Store User", Emails = [new Email(email)] };

    [Fact]
    public async Task CreateAsync_PersistsUser_AndAuditsCreationOnce()
    {
        var user = NewUser("store-create@test.com");

        await using (var ctx = Audited())
        {
            var result = await StoreFor(ctx).CreateAsync(user, CancellationToken.None);
            Assert.True(result.Succeeded);
        }

        var actions = await ActionsAsync("User", user.Id);
        Assert.IsType<AuditLogEntry.Created>(Assert.Single(actions));
    }

    [Fact]
    public async Task CreateAsync_AssignsUsersRoleWithReadGrants()
    {
        var user = NewUser("store-role@test.com");

        await using (var ctx = Audited())
        {
            Assert.True((await StoreFor(ctx).CreateAsync(user, CancellationToken.None)).Succeeded);
        }

        await using var read = _factory.CreateContext();
        var roles = await new RoleQueries(read).GetRolesByUserId(user.Id);
        var role = Assert.Single(roles);
        Assert.Equal(DomainConstants.UsersRole, role.Name);
        Assert.True(await new UserQueries(read).HasGrant("store-role@test.com", "AuditLog.Read"));
        Assert.False(await new UserQueries(read).HasGrant("store-role@test.com", "AuditLog.Write"));
        Assert.False(await new UserQueries(read).HasGrant("store-role@test.com", "Configuration.Read"));
    }

    [Fact]
    public async Task UpdateAsync_PersistsDisplayName_AndAuditsDifference()
    {
        var user = NewUser("store-update@test.com");
        await using (var ctx = Audited())
        {
            await StoreFor(ctx).CreateAsync(user, CancellationToken.None);
        }

        user.DisplayName = "Renamed";
        await using (var ctx = Audited())
        {
            var result = await StoreFor(ctx).UpdateAsync(user, CancellationToken.None);
            Assert.True(result.Succeeded);
        }

        await using var verify = _factory.CreateContext();
        var stored = await verify.Users.SingleAsync(u => EF.Property<Guid>(u, "Id") == user.Id);
        Assert.Equal("Renamed", stored.DisplayName);

        var updated = Assert.IsType<AuditLogEntry.Updated>(Assert.Single(await ActionsAsync("User", user.Id), a => a is AuditLogEntry.Updated));
        Assert.Contains(updated.Differences, d => d.PropertyName == nameof(HumanUser.DisplayName));
    }

    [Fact]
    public async Task UpdateAsync_UnknownUser_Fails()
    {
        await using var ctx = Audited();

        var result = await StoreFor(ctx).UpdateAsync(NewUser("nobody@test.com"), CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task DeleteAsync_RemovesUser_AndAuditsDeletion()
    {
        var user = NewUser("store-delete@test.com");
        await using (var ctx = Audited())
        {
            await StoreFor(ctx).CreateAsync(user, CancellationToken.None);
        }

        await using (var ctx = Audited())
        {
            var result = await StoreFor(ctx).DeleteAsync(user, CancellationToken.None);
            Assert.True(result.Succeeded);
        }

        await using var verify = _factory.CreateContext();
        Assert.False(await verify.Users.AnyAsync(u => EF.Property<Guid>(u, "Id") == user.Id));
        Assert.Contains(await ActionsAsync("User", user.Id), a => a is AuditLogEntry.Deleted);
    }

    [Fact]
    public async Task DeleteAsync_UserWhoActedBefore_KeepsAuditTrail()
    {
        var user = NewUser("store-actor-delete@test.com");
        await using (var ctx = _factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            await StoreFor(ctx).CreateAsync(user, CancellationToken.None);
        }

        await using (var ctx = _factory.CreateAuditedContext(AuditActor.ForUser(user.Id)))
        {
            ctx.Grants.Add(new Grant("created-by-soon-deleted-user"));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _factory.CreateAuditedContext(AuditActor.ForUser(user.Id)))
        {
            var result = await StoreFor(ctx).DeleteAsync(user, CancellationToken.None);
            Assert.True(result.Succeeded);
        }

        await using var verify = _factory.CreateContext();
        Assert.False(await verify.Users.AnyAsync(u => EF.Property<Guid>(u, "Id") == user.Id));
        Assert.Contains(await ActionsAsync("User", user.Id), a => a is AuditLogEntry.Deleted);
    }

    [Fact]
    public async Task Role_Create_Update_Delete_ArePersistedAndAudited()
    {
        var role = new ApplicationRole { Name = "editor" };

        await using (var ctx = Audited())
        {
            Assert.True((await StoreFor(ctx).CreateAsync(role, CancellationToken.None)).Succeeded);
        }

        role.Name = "chief-editor";
        await using (var ctx = Audited())
        {
            Assert.True((await StoreFor(ctx).UpdateAsync(role, CancellationToken.None)).Succeeded);
        }

        await using (var verify = _factory.CreateContext())
        {
            Assert.Equal("chief-editor", (await verify.Roles.SingleAsync(r => EF.Property<Guid>(r, "Id") == role.Id)).Name);
        }

        await using (var ctx = Audited())
        {
            Assert.True((await StoreFor(ctx).DeleteAsync(role, CancellationToken.None)).Succeeded);
        }

        Assert.Collection(
            await ActionsAsync("Role", role.Id),
            a => Assert.IsType<AuditLogEntry.Created>(a),
            a => Assert.IsType<AuditLogEntry.Updated>(a),
            a => Assert.IsType<AuditLogEntry.Deleted>(a));
    }

    [Fact]
    public async Task ConfirmEmail_PersistsAndAuditsConfirmation()
    {
        var user = NewUser("store-confirm@test.com");
        await using (var ctx = Audited())
        {
            await StoreFor(ctx).CreateAsync(user, CancellationToken.None);
        }

        await using (var ctx = Audited())
        {
            await new EmailCommands(ctx, TimeProvider.System).Confirm("store-confirm@test.com");
        }

        await using var verify = _factory.CreateContext();
        Assert.True(await verify.Emails.AnyAsync(e => e.Value == "store-confirm@test.com" && e.ConfirmedAt != null));
        Assert.Contains(
            await verify.AuditLogEntries.Where(a => a.EntityName == nameof(Email)).Select(a => a.Action).ToListAsync(),
            a => a is AuditLogEntry.Updated);
    }

    [Fact]
    public async Task InactivatePasswords_PersistsAndAuditsWithoutLeakingHash()
    {
        var user = NewUser("store-password@test.com");
        await using (var ctx = Audited())
        {
            await StoreFor(ctx).CreateAsync(user, CancellationToken.None);
            await new PasswordCommands(ctx).CreateFromUserId(user.Id, new Password("hash-one", true));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = Audited())
        {
            await new PasswordCommands(ctx).InactivateFromUserId(user.Id);
            await ctx.SaveChangesAsync();
        }

        await using var verify = _factory.CreateContext();
        Assert.False(await verify.Passwords.AnyAsync(p => p.IsActive));

        var serialized = await verify.Database
            .SqlQuery<string>($"SELECT Action AS Value FROM AuditLogEntries WHERE EntityName = 'Password'")
            .ToListAsync();
        Assert.NotEmpty(serialized);
        Assert.DoesNotContain(serialized, value => value.Contains("hash-one"));
    }

    [Fact]
    public async Task SetEmailAsync_ChangesPrimaryEmail_ResetsConfirmation_AndAudits()
    {
        var user = NewUser("store-old-email@test.com");
        await using (var ctx = Audited())
        {
            await StoreFor(ctx).CreateAsync(user, CancellationToken.None);
            await new EmailCommands(ctx, TimeProvider.System).Confirm("store-old-email@test.com");
        }

        await using (var ctx = Audited())
        {
            await StoreFor(ctx).SetEmailAsync(user, "Store-New-Email@Test.com", CancellationToken.None);
            await ctx.SaveChangesAsync();
        }

        await using var verify = _factory.CreateContext();
        var emails = await verify.Emails
            .Where(e => EF.Property<Guid>(e, "UserId") == user.Id)
            .ToListAsync();
        var email = Assert.Single(emails);
        Assert.Equal("store-new-email@test.com", email.Value);
        Assert.Null(email.ConfirmedAt);
        Assert.Contains(
            await verify.AuditLogEntries.Where(a => a.EntityName == nameof(Email)).Select(a => a.Action).ToListAsync(),
            a => a is AuditLogEntry.Updated updated && updated.Differences.Any(d => d.PropertyName == nameof(Email.Value)));
    }

    [Fact]
    public async Task SetEmailAsync_ExistingEmail_DoesNothing()
    {
        var user = NewUser("store-same-email@test.com");
        await using (var ctx = Audited())
        {
            await StoreFor(ctx).CreateAsync(user, CancellationToken.None);
        }

        await using (var ctx = Audited())
        {
            await StoreFor(ctx).SetEmailAsync(user, "STORE-SAME-EMAIL@test.com", CancellationToken.None);
            Assert.Equal(0, await ctx.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task SetEmailAsync_UserWithoutEmail_AddsEmail()
    {
        Guid userId;
        await using (var seed = _factory.CreateContext())
        {
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "store-seeded@test.com");
            await seed.Database.ExecuteSqlAsync($"DELETE FROM Emails WHERE UserId = {userId}");
        }

        await using (var ctx = Audited())
        {
            await StoreFor(ctx).SetEmailAsync(new ApplicationUser { Id = userId }, "store-added@test.com", CancellationToken.None);
            await ctx.SaveChangesAsync();
        }

        await using var verify = _factory.CreateContext();
        Assert.True(await verify.Emails.AnyAsync(e => e.Value == "store-added@test.com" && EF.Property<Guid>(e, "UserId") == userId));
    }

    [Fact]
    public async Task SetEmailAsync_NullEmail_DoesNothing()
    {
        await using var ctx = Audited();

        await StoreFor(ctx).SetEmailAsync(NewUser("store-null@test.com"), null, CancellationToken.None);

        Assert.Equal(0, await ctx.SaveChangesAsync());
    }

    private async Task<ApplicationUser> CreatedUserAsync(string email)
    {
        var user = NewUser(email);
        await using var ctx = Audited();
        await StoreFor(ctx).CreateAsync(user, CancellationToken.None);
        return user;
    }

    [Fact]
    public async Task SetEmailConfirmedAsync_PersistsConfirmation_AndGetEmailConfirmedReadsIt()
    {
        var user = await CreatedUserAsync("store-confirmed@test.com");

        await using (var ctx = Audited())
        {
            var store = StoreFor(ctx);
            Assert.False(await store.GetEmailConfirmedAsync(user, CancellationToken.None));

            await store.SetEmailConfirmedAsync(user, true, CancellationToken.None);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = Audited())
        {
            var store = StoreFor(ctx);
            Assert.True(await store.GetEmailConfirmedAsync(user, CancellationToken.None));

            await store.SetEmailConfirmedAsync(user, false, CancellationToken.None);
            await ctx.SaveChangesAsync();
        }

        await using var verify = _factory.CreateContext();
        Assert.False(await verify.Emails.AnyAsync(e => e.ConfirmedAt != null && EF.Property<Guid>(e, "UserId") == user.Id));
        var updates = (await verify.AuditLogEntries.Where(a => a.EntityName == nameof(Email)).Select(a => a.Action).ToListAsync())
            .OfType<AuditLogEntry.Updated>()
            .ToList();
        Assert.Equal(2, updates.Count);
    }

    [Fact]
    public async Task SetPasswordHashAsync_ReplacesActivePassword_AndAuditsWithoutLeakingHash()
    {
        var user = await CreatedUserAsync("store-set-password@test.com");

        foreach (var hash in new[] { "first-hash", "second-hash" })
        {
            await using var ctx = Audited();
            await StoreFor(ctx).SetPasswordHashAsync(user, hash, CancellationToken.None);
            await ctx.SaveChangesAsync();
        }

        await using var verify = _factory.CreateContext();
        var passwords = await verify.Passwords
            .Where(p => EF.Property<Guid>(p, "UserId") == user.Id)
            .ToListAsync();
        Assert.Equal(2, passwords.Count);
        Assert.Equal("second-hash", Assert.Single(passwords, p => p.IsActive).Value);

        var serialized = await verify.Database
            .SqlQuery<string>($"SELECT Action AS Value FROM AuditLogEntries WHERE EntityName = 'Password'")
            .ToListAsync();
        Assert.DoesNotContain(serialized, value => value.Contains("first-hash") || value.Contains("second-hash"));
    }

    [Fact]
    public async Task SetPasswordHashAsync_Null_RemovesActivePassword()
    {
        var user = await CreatedUserAsync("store-remove-password@test.com");
        await using (var ctx = Audited())
        {
            await StoreFor(ctx).SetPasswordHashAsync(user, "to-remove", CancellationToken.None);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = Audited())
        {
            var store = StoreFor(ctx);
            Assert.True(await store.HasPasswordAsync(user, CancellationToken.None));

            await store.SetPasswordHashAsync(user, null, CancellationToken.None);
            await ctx.SaveChangesAsync();
        }

        await using var verify = _factory.CreateContext();
        Assert.False(await StoreFor(verify).HasPasswordAsync(user, CancellationToken.None));
    }

    [Fact]
    public async Task UserBasedCommandsAndQueries_ResolveDetachedUserThroughEmails()
    {
        var user = await CreatedUserAsync("store-detached@test.com");
        var detached = new HumanUser("Detached", [new Email("Store-Detached@test.com")], None);

        await using (var ctx = Audited())
        {
            await new PasswordCommands(ctx).CreateForUser(detached, new Password("detached-hash", true));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = Audited())
        {
            Assert.Single(await new PasswordQueries(ctx).GetPasswordsForUser(detached));
            Assert.Single(await new EmailQueries(ctx).GetEmailsByUser(detached));

            await new PasswordCommands(ctx).InactivateForUser(detached);
            await ctx.SaveChangesAsync();
        }

        await using var verify = _factory.CreateContext();
        Assert.False(await verify.Passwords.AnyAsync(p => EF.Property<Guid>(p, "UserId") == user.Id && p.IsActive));
    }

    [Fact]
    public async Task UserBasedLookups_UnknownOrNonHumanUser_Throw()
    {
        await using var ctx = Audited();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EmailQueries(ctx).GetEmailsByUser(new HumanUser("Ghost", [new Email("ghost@test.com")], None)));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new PasswordQueries(ctx).GetPasswordsForUser(new SystemUser(None)));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task Confirm_StoresConfirmationTime_AndKeepsTheFirstOne()
    {
        var user = await CreatedUserAsync("store-confirmed-at@test.com");
        var firstTime = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(firstTime);

        await using (var ctx = Audited())
        {
            await new EmailCommands(ctx, timeProvider).Confirm("store-confirmed-at@test.com");
        }

        timeProvider.Now = firstTime.AddDays(5);
        await using (var ctx = Audited())
        {
            await new EmailCommands(ctx, timeProvider).Confirm("store-confirmed-at@test.com");
            await new EmailCommands(ctx, timeProvider).SetConfirmed(user.Id, true);
            await ctx.SaveChangesAsync();
        }

        await using var verify = _factory.CreateContext();
        var email = await verify.Emails.SingleAsync(e => EF.Property<Guid>(e, "UserId") == user.Id);
        Assert.Equal(firstTime, email.ConfirmedAt);
    }

    [Fact]
    public async Task SetConfirmed_False_ClearsConfirmationTime()
    {
        var user = await CreatedUserAsync("store-unconfirmed-at@test.com");
        var timeProvider = new FixedTimeProvider(DateTimeOffset.UtcNow);

        await using (var ctx = Audited())
        {
            await new EmailCommands(ctx, timeProvider).SetConfirmed(user.Id, true);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = Audited())
        {
            await new EmailCommands(ctx, timeProvider).SetConfirmed(user.Id, false);
            await ctx.SaveChangesAsync();
        }

        await using var verify = _factory.CreateContext();
        var email = await verify.Emails.SingleAsync(e => EF.Property<Guid>(e, "UserId") == user.Id);
        Assert.Null(email.ConfirmedAt);
    }
}
