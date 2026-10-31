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
            new PasswordQueries(context),
            new RoleQueries(context),
            new PasswordCommands(context),
            new UserCommands(context),
            new RoleCommands(context));

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
            await new EmailCommands(ctx).Confirm("store-confirm@test.com");
        }

        await using var verify = _factory.CreateContext();
        Assert.True(await verify.Emails.AnyAsync(e => e.Value == "store-confirm@test.com" && e.IsConfirmed));
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
}
