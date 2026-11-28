using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Behind.Common.Database.Modules;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.Domain;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Behind.Tests.CoreDatabase;

public sealed class ServiceUserStoreTests : IDisposable
{
    private readonly NewsTestBed _bed = new();
    private static readonly CancellationToken Token = CancellationToken.None;

    public void Dispose() => _bed.Dispose();

    private async Task SeedRoles()
    {
        await using var context = _bed.Read();
        await context.EnsureBuiltInRolesAsync(AccessCatalog.Core);
    }

    [Fact]
    public async Task CredentialsAndOptimisticUpdatesUseIndependentIdentity()
    {
        await SeedRoles();
        await using var context = _bed.Write();
        var store = new ServiceUserStore(context, TimeProvider.System);
        Assert.True((await store.Create(" ", Token)).IsNone());
        var credential = (await store.Create(" agent ", Token))
            .Match(() => throw new InvalidOperationException(), value => value);
        var id = credential.User.Id;
        Assert.StartsWith($"behind_{id:N}_", credential.ApiKey);
        Assert.Equal("agent", credential.User.User.Name);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credential.ApiKey))), credential.User.User.ApiKeyHash);
        Assert.DoesNotContain(credential.User.User.ApiKeyHash, JsonSerializer.Serialize(credential.User.User));
        var authenticated = await store.Authenticate(credential.ApiKey, Token);
        Assert.False(authenticated.IsNone());
        Assert.Equal(id, authenticated.Match(() => Guid.Empty, entry => entry.Id));
        Assert.True((await store.Authenticate(credential.ApiKey + "x", Token)).IsNone());
        Assert.Contains((await context.GetUserAccessAsync(id)).Roles, role => role.Name == BuiltInAccess.Users.Role.Name);
        var rotated = (await store.Rotate(id, 1, Token)).Match(() => throw new InvalidOperationException(), value => value);
        Assert.True((await store.Authenticate(credential.ApiKey, Token)).IsNone());
        Assert.False((await store.Authenticate(rotated.ApiKey, Token)).IsNone());
        Assert.True((await store.Rotate(id, 1, Token)).IsNone());
        Assert.False(await store.Delete(id, 1, Token));
        Assert.True(await store.SetEnabled(id, 2, false, Token));
        Assert.True((await store.Authenticate(rotated.ApiKey, Token)).IsNone());
        Assert.True(await store.SetEnabled(id, 3, true, Token));
        Assert.True(await store.Delete(id, 4, Token));
        Assert.True((await store.Find(id, Token)).IsNone());
        Assert.True((await store.Authenticate(rotated.ApiKey, Token)).IsNone());
        Assert.Single(await store.List(true, Token));
        Assert.True(await store.Restore(id, 4, Token));
        Assert.True((await store.Authenticate(rotated.ApiKey, Token)).IsNone());
        Assert.True(await store.SetEnabled(id, 5, true, Token));
        Assert.True((await store.Authenticate(rotated.ApiKey, Token)).IsNone());
        Assert.True(await store.Delete(id, 6, Token));
        Assert.True(await store.HardDelete(id, 6, Token));
        Assert.False(await store.Restore(id, 6, Token));
    }

    [Fact]
    public async Task ActiveBanImmediatelyBlocksAuthentication()
    {
        await SeedRoles();
        await using var context = _bed.Write();
        var store = new ServiceUserStore(context, TimeProvider.System);
        var credential = (await store.Create("agent", Token))
            .Match(() => throw new InvalidOperationException(), value => value);
        var bans = new WallOfShameCommands(context);
        await bans.Ban(credential.User.Id, _bed.OtherAuthorId, new Reason("test"));
        Assert.True((await store.Authenticate(credential.ApiKey, Token)).IsNone());
        await bans.Unban(credential.User.Id);
        Assert.False((await store.Authenticate(credential.ApiKey, Token)).IsNone());
    }

    [Fact]
    public async Task AccessChangesApplyToServiceIdentityAndNotOtherUsers()
    {
        await SeedRoles();
        await using var context = _bed.Write();
        var store = new ServiceUserStore(context, TimeProvider.System);
        var credential = (await store.Create("agent", Token))
            .Match(() => throw new InvalidOperationException(), value => value);
        var grant = BuiltInAccess.RawDatabaseManage.Grant.Name;
        var restriction = BuiltInAccess.Restrictions[0].Restriction.Name;
        await new AccessCommands(context).SetUserAccess(credential.User.Id, [BuiltInAccess.Users.Id],
            new AccessSelection([grant], [restriction]), Token);
        var access = await context.GetUserAccessAsync(credential.User.Id, Token);
        Assert.Contains(access.Grants, value => value.Name == grant);
        Assert.Contains(access.Restrictions, value => value.Name == restriction);
        Assert.DoesNotContain((await context.GetUserAccessAsync(_bed.AuthorId, Token)).Grants, value => value.Name == grant);
    }

    [Fact]
    public async Task HistoryPreventsPermanentDeletion()
    {
        await SeedRoles();
        await using var context = _bed.Write();
        var store = new ServiceUserStore(context, TimeProvider.System);
        var credential = (await store.Create("agent", Token))
            .Match(() => throw new InvalidOperationException(), value => value);
        var id = credential.User.Id;
        await using (var seed = _bed.Read())
        {
            var audit = new AuditLogEntry("Example", Guid.NewGuid().ToString(), new AuditLogEntry.Created(), DateTimeOffset.UtcNow);
            await seed.AuditLogEntries.AddAsync(audit);
            seed.Entry(audit).Property<Guid?>("UserId").CurrentValue = id;
            await seed.SaveChangesAsync();
        }
        Assert.True(await store.Delete(id, 1, Token));
        Assert.False(await store.HardDelete(id, 2, Token));
        Assert.Single(await store.List(true, Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UserHierarchyPreservesAuthoredContentDeletionAndRestoration(bool service)
    {
        await SeedRoles();
        await using var context = _bed.Write();
        var store = new ServiceUserStore(context, TimeProvider.System);
        var id = service
            ? (await store.Create("agent", Token)).Match(() => throw new InvalidOperationException(), value => value.User.Id)
            : _bed.OtherAuthorId;
        var newsId = Guid.NewGuid();
        var news = new News("Authored", "Summary", "Content", false);
        await context.NewsEntries.AddAsync(news);
        context.Entry(news).StampVersionedKey(newsId, 1);
        context.Entry(news).Property<Guid>(NewsConfiguration.AuthorIdProperty).CurrentValue = id;
        await context.SaveChangesAsync();
        if (service)
        {
            Assert.True(await store.Delete(id, 1, Token));
        }
        else
        {
            await new UserCommands(context).Delete(id, Token);
        }
        Assert.False(await context.NewsEntries.AnyAsync(value => EF.Property<Guid>(value, VersionedColumns.Key) == newsId));
        if (service)
        {
            Assert.True(await store.Restore(id, 1, Token));
        }
        else
        {
            await new UserCommands(context).Restore(id, Token);
        }
        Assert.True(await context.NewsEntries.AnyAsync(value => EF.Property<Guid>(value, VersionedColumns.Key) == newsId));
    }

    [Fact]
    public async Task ServiceLabelsAppearInProfilesAndActiveBans()
    {
        await SeedRoles();
        await using var context = _bed.Write();
        var store = new ServiceUserStore(context, TimeProvider.System);
        var credential = (await store.Create("agent", Token))
            .Match(() => throw new InvalidOperationException(), value => value);
        var id = credential.User.Id;
        var expected = id.ToUserLabel("agent");
        var profiles = new UserProfiles(context, Substitute.For<IModuleFiles>());
        Assert.Equal(expected, Assert.Single(await profiles.Find([id], Token)).DisplayName);
        var profile = await profiles.GetPublic(id, Token);
        Assert.NotNull(profile);
        Assert.Equal(expected, profile.DisplayName);
        Assert.Null(profile.PrimaryEmail);
        Assert.Null(profile.PictureId);
        var evidence = await new CoreReportEvidenceQueries(context).Find("profile", id, TimeProvider.System.GetUtcNow(), Token);
        Assert.NotNull(evidence);
        Assert.Equal(id, evidence.AuthorId);
        Assert.Equal("agent", evidence.Content);
        await new WallOfShameCommands(context).Ban(id, _bed.AuthorId, new Reason("test"));
        var page = await new WallOfShameQueries(context).GetActive(new Flipper(1, 10), TimeProvider.System.GetUtcNow(), Token);
        Assert.Equal(expected, Assert.Single(page.Results.Match(() => [], rows => rows.ToList())).DisplayName);
    }
}
