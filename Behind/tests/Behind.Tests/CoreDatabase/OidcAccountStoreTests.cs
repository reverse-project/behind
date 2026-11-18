using Behind.Common.Database;
using Behind.Common.Database.Raw;
using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Sqlite;
using Behind.Core.Domain;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Tests.CoreDatabase;

public sealed class OidcAccountStoreTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly AuditActor _actor;

    public OidcAccountStoreTests()
    {
        using var context = _factory.CreateContext();
        var actorId = DatabaseSeedHelper.SeedHumanUserAsync(context, "oidc-test-actor@test.com")
            .GetAwaiter()
            .GetResult();
        _actor = AuditActor.ForUser(actorId);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task AdministrativeRoleSaveRetainsProviderRoleAfterClaimRemoval()
    {
        var userId = await SeedUserAsync("saved-role@test.com");
        var roleId = await SeedRoleAsync("saved-role");
        await WithStoreAsync(store => store.SynchronizeRoles(userId, "provider", ["saved-role"], [], CancellationToken.None));
        await using (var context = _factory.CreateContext())
        {
            await context.ReplaceUserAccessAsync(userId, [roleId], new AccessSelection([], []));
        }
        await WithStoreAsync(store => store.SynchronizeRoles(userId, "provider", [], [], CancellationToken.None));
        Assert.True(await HasMembershipAsync(userId, roleId));
        Assert.True(await IsManualMembershipAsync(userId, roleId));
    }

    [Fact]
    public async Task Link_UsesProviderIssuerAndSubjectAndEnforcesUniqueBinding()
    {
        var firstUser = await SeedUserAsync("first@test.com");
        var secondUser = await SeedUserAsync("second@test.com");

        await WithStoreAsync(async store =>
        {
            await store.Link(firstUser, "provider-a", "issuer-a", "subject", CancellationToken.None);
            await store.Link(secondUser, "provider-b", "issuer-a", "subject", CancellationToken.None);
            await store.Link(secondUser, "provider-a", "issuer-b", "subject", CancellationToken.None);
            await store.Link(secondUser, "provider-a", "issuer-a", "subject-2", CancellationToken.None);
            await store.Link(firstUser, "provider-a", "issuer-a", "subject", CancellationToken.None);

            Assert.Equal(firstUser, await store.FindUser(
                "provider-a", "issuer-a", "subject", CancellationToken.None));
            Assert.Equal(secondUser, await store.FindUser(
                "provider-b", "issuer-a", "subject", CancellationToken.None));
            Assert.Equal(secondUser, await store.FindUser(
                "provider-a", "issuer-b", "subject", CancellationToken.None));
            Assert.Equal(secondUser, await store.FindUser(
                "provider-a", "issuer-a", "subject-2", CancellationToken.None));
            Assert.Null(await store.FindUser(
                "provider-a", "issuer-a", "unknown", CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.Link(
                secondUser, "provider-a", "issuer-a", "subject", CancellationToken.None));
        });

        await using var verify = _factory.CreateContext();
        var binding = await verify.Set<Dictionary<string, object>>("OidcAccountBindings").FirstAsync();
        Assert.False(binding.ContainsKey("Subject"));
        Assert.Equal(64, ((string)binding["SubjectDigest"]).Length);
    }

    [Fact]
    public async Task CreateAndLink_RollsBackTheNewUserWhenLinkingFails()
    {
        var owner = await SeedUserAsync("owner@test.com");
        await WithStoreAsync(store => store.Link(owner, "provider", "issuer", "subject", CancellationToken.None));
        var newUser = Guid.NewGuid();

        await using (var context = _factory.CreateContext())
        {
            var store = new OidcAccountStore(context);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.CreateAndLink(
                async () =>
                {
                    await CreateUserAsync(context, newUser, "new@test.com");
                    return true;
                },
                newUser, "provider", "issuer", "subject", CancellationToken.None));
        }

        Assert.False(await UserExistsAsync(newUser));
    }

    [Fact]
    public async Task CreateAndLink_CommitsTheUserWithItsBindingOrRollsBackARejectedCreation()
    {
        var rejected = Guid.NewGuid();
        var accepted = Guid.NewGuid();

        await using (var context = _factory.CreateContext())
        {
            var store = new OidcAccountStore(context);
            Assert.False(await store.CreateAndLink(
                async () =>
                {
                    await CreateUserAsync(context, rejected, "rejected@test.com");
                    return false;
                },
                rejected, "provider", "issuer", "rejected", CancellationToken.None));
            Assert.True(await store.CreateAndLink(
                async () =>
                {
                    await CreateUserAsync(context, accepted, "accepted@test.com");
                    return true;
                },
                accepted, "provider", "issuer", "accepted", CancellationToken.None));
        }

        Assert.False(await UserExistsAsync(rejected));
        Assert.True(await UserExistsAsync(accepted));
        await WithStoreAsync(async store =>
        {
            Assert.Null(await store.FindUser("provider", "issuer", "rejected", CancellationToken.None));
            Assert.Equal(accepted, await store.FindUser("provider", "issuer", "accepted", CancellationToken.None));
        });
    }

    [Fact]
    public async Task FindUser_ReturnsLinkedIdForSoftDeletedUserAndLinkCannotReassignIt()
    {
        var deletedUser = await SeedUserAsync("deleted@test.com");
        var activeUser = await SeedUserAsync("active@test.com");

        await WithStoreAsync(store => store.Link(
            deletedUser, "provider", "issuer", "subject", CancellationToken.None));
        await DeleteUserAsync(deletedUser);

        await WithStoreAsync(async store =>
        {
            Assert.Equal(deletedUser, await store.FindUser(
                "provider", "issuer", "subject", CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.Link(
                activeUser, "provider", "issuer", "subject", CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.SynchronizeRoles(
                deletedUser, "provider", [], [], CancellationToken.None));
        });
    }

    [Fact]
    public async Task SynchronizeRoles_RequiresMappedRolesButAllowsMappedPrivilegedRoles()
    {
        var userId = await SeedUserAsync("mapped@test.com");
        var roleId = await SeedRoleAsync("Administrator");
        await using (var context = _factory.CreateContext())
        {
            var grantId = await DatabaseSeedHelper.SeedGrantAsync(context, "users.manage");
            await DatabaseSeedHelper.AssignGrantToRoleAsync(context, roleId, grantId);
        }

        await WithStoreAsync(async store =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.SynchronizeRoles(
                userId, "provider", ["Missing"], ["oidc:provider:external"], CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.SynchronizeRoles(
                userId,
                "provider",
                [],
                ["oidc:provider:new", "Administrator"],
                CancellationToken.None));

            await store.SynchronizeRoles(
                userId, "provider", ["Administrator"], [], CancellationToken.None);
        });

        Assert.True(await HasMembershipAsync(userId, roleId));
        Assert.False(await HasRoleAsync("oidc:provider:external"));
        Assert.False(await HasRoleAsync("oidc:provider:new"));
    }

    [Fact]
    public async Task SynchronizeRoles_MatchesAndCreatesNamesWithExactCase()
    {
        var userId = await SeedUserAsync("role-case@test.com");
        var privilegedRole = await SeedRoleAsync("Editor");
        await WithStoreAsync(async store =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.SynchronizeRoles(
                userId, "provider", ["editor"], [], CancellationToken.None));
            await store.SynchronizeRoles(userId, "provider", ["Editor"], ["editor", "EDITOR"], CancellationToken.None);
        });

        var lower = await FindRoleAsync("editor");
        var upper = await FindRoleAsync("EDITOR");
        Assert.NotEqual(lower, upper);
        Assert.True(await HasMembershipAsync(userId, privilegedRole));
        Assert.True(await HasMembershipAsync(userId, lower));
        Assert.True(await HasMembershipAsync(userId, upper));
    }

    [Fact]
    public async Task SynchronizeRoles_CreatesOnlyOrdinaryAutoRolesAndRemovesProviderOwnedMemberships()
    {
        var userId = await SeedUserAsync("auto@test.com");
        var existingRoleId = await SeedRoleAsync("oidc:provider:existing");

        await WithStoreAsync(store => store.SynchronizeRoles(
            userId,
            "provider",
            [],
            ["oidc:provider:auditor", "oidc:provider:existing"],
            CancellationToken.None));

        var roleId = await FindRoleAsync("oidc:provider:auditor");
        Assert.NotEqual(Guid.Empty, roleId);
        Assert.Equal(existingRoleId, await FindRoleAsync("oidc:provider:existing"));
        Assert.False(await IsDeveloperAsync(roleId));
        Assert.False(await HasGrantAsync(roleId));
        Assert.True(await HasMembershipAsync(userId, roleId));
        Assert.True(await HasMembershipAsync(userId, existingRoleId));

        await WithStoreAsync(store => store.SynchronizeRoles(
            userId, "provider", [], [], CancellationToken.None));

        Assert.False(await HasMembershipAsync(userId, roleId));
        Assert.False(await HasMembershipAsync(userId, existingRoleId));
    }

    [Fact]
    public async Task SynchronizeRoles_RejectsPrivilegedExistingAutoRoleAndDeletedRole()
    {
        var userId = await SeedUserAsync("unsafe-auto@test.com");
        var grantedRoleId = await SeedRoleAsync("oidc:provider:granted");
        var developerRoleId = await SeedRoleAsync("oidc:provider:developer");
        await using (var context = _factory.CreateContext())
        {
            var grantId = await DatabaseSeedHelper.SeedGrantAsync(context, "private.data");
            await DatabaseSeedHelper.AssignGrantToRoleAsync(context, grantedRoleId, grantId);
            context.Entry(await context.Roles.FirstAsync(role =>
                EF.Property<Guid>(role, "Id") == developerRoleId))
                .Property(role => role.IsDeveloper).CurrentValue = true;
            await context.SaveChangesAsync();
        }

        await WithStoreAsync(async store =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.SynchronizeRoles(
                userId, "provider", [], ["oidc:provider:granted"], CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.SynchronizeRoles(
                userId, "provider", [], ["oidc:provider:developer"], CancellationToken.None));
        });

        var deletedRoleId = await SeedRoleAsync("oidc:provider:équipe");
        await DeleteRoleAsync(deletedRoleId);
        await WithStoreAsync(store => Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.SynchronizeRoles(userId, "provider", [], ["oidc:provider:équipe"], CancellationToken.None)));
        Assert.False(await HasMembershipAsync(userId, deletedRoleId));
    }

    [Fact]
    public async Task SynchronizeRoles_UnionsProviderOwnershipAndPreservesManualAssignments()
    {
        var userId = await SeedUserAsync("ownership@test.com");
        var sharedRoleId = await SeedRoleAsync("shared");
        var manuallyKeptRoleId = await SeedRoleAsync("manual");

        await WithStoreAsync(async store =>
        {
            await store.SynchronizeRoles(userId, "provider-a", ["shared"], [], CancellationToken.None);
            await store.SynchronizeRoles(userId, "provider-b", ["shared"], [], CancellationToken.None);
            await store.SynchronizeRoles(userId, "provider-a", [], [], CancellationToken.None);
        });
        Assert.True(await HasMembershipAsync(userId, sharedRoleId));

        await using (var context = _factory.CreateContext())
        {
            await context.RemoveRoleFromUserAsync(userId, sharedRoleId, CancellationToken.None);
        }
        Assert.True(await HasMembershipAsync(userId, sharedRoleId));

        await WithStoreAsync(store => store.SynchronizeRoles(
            userId, "provider-b", [], [], CancellationToken.None));
        Assert.False(await HasMembershipAsync(userId, sharedRoleId));

        await WithStoreAsync(store => store.SynchronizeRoles(
            userId, "provider-a", ["manual"], [], CancellationToken.None));

        await using (var context = _factory.CreateContext())
        {
            await context.AssignRoleToUserAsync(userId, manuallyKeptRoleId, CancellationToken.None);
        }

        await WithStoreAsync(store => store.SynchronizeRoles(
            userId, "provider-a", [], [], CancellationToken.None));

        Assert.True(await HasMembershipAsync(userId, manuallyKeptRoleId));
        Assert.True(await IsManualMembershipAsync(userId, manuallyKeptRoleId));
    }

    [Fact]
    public void OidcInfrastructureTablesAreReadOnlyInRawDatabase()
    {
        using var context = _factory.CreateContext();

        Assert.True(context.IsReadOnly(context.FindRawTable("OidcAccountBindings")
            .Match(() => throw new InvalidOperationException(), table => table)));
        Assert.True(context.IsReadOnly(context.FindRawTable("OidcRoleOwnerships")
            .Match(() => throw new InvalidOperationException(), table => table)));
    }

    private async Task<Guid> SeedUserAsync(string email)
    {
        await using var context = _factory.CreateContext();
        return await DatabaseSeedHelper.SeedHumanUserAsync(context, email);
    }

    private static async Task CreateUserAsync(CoreContextSqlite context, Guid id, string email)
    {
        var user = new HumanUser("Test User", [new Email(email)], None);
        await context.Users.AddAsync(user);
        context.Entry(user).Property<Guid>("Id").CurrentValue = id;
        await context.SaveChangesAsync();
    }

    private async Task<bool> UserExistsAsync(Guid userId)
    {
        await using var context = _factory.CreateContext();
        using var scope = context.SoftDelete.IncludeDeleted();
        return await context.Users.AnyAsync(user => EF.Property<Guid>(user, "Id") == userId);
    }

    private async Task<Guid> SeedRoleAsync(string name)
    {
        await using var context = _factory.CreateContext();
        return await DatabaseSeedHelper.SeedUserRoleAsync(context, name);
    }

    private async Task<T> WithStoreAsync<T>(Func<OidcAccountStore, Task<T>> action)
    {
        await using var context = _factory.CreateContext();
        return await action(new OidcAccountStore(context));
    }

    private Task WithStoreAsync(Func<OidcAccountStore, Task> action) =>
        WithStoreAsync(async store =>
        {
            await action(store);
            return true;
        });

    private async Task DeleteUserAsync(Guid userId)
    {
        await using var context = _factory.CreateAuditedContext(_actor);
        await context.GetRepository<HumanUser, Guid>().DeleteAsync(userId, CancellationToken.None);
        await context.SaveChangesAsync();
    }

    private async Task DeleteRoleAsync(Guid roleId)
    {
        await using var context = _factory.CreateAuditedContext(_actor);
        await context.GetRepository<UserRole, Guid>().DeleteAsync(roleId, CancellationToken.None);
        await context.SaveChangesAsync();
    }

    private async Task<bool> HasMembershipAsync(Guid userId, Guid roleId)
    {
        await using var context = _factory.CreateContext();
        return await context.Set<Dictionary<string, object>>("UserRoles").AnyAsync(row =>
            EF.Property<Guid>(row, "UserId") == userId && EF.Property<Guid>(row, "RoleId") == roleId);
    }

    private async Task<bool> IsManualMembershipAsync(Guid userId, Guid roleId)
    {
        await using var context = _factory.CreateContext();
        var membership = await context.Set<Dictionary<string, object>>("UserRoles").SingleAsync(row =>
            EF.Property<Guid>(row, "UserId") == userId && EF.Property<Guid>(row, "RoleId") == roleId);
        return (bool)membership["IsManuallyAssigned"];
    }

    private async Task<Guid> FindRoleAsync(string name)
    {
        await using var context = _factory.CreateContext();
        return await context.Roles
            .Where(role => role.Name == name)
            .Select(role => EF.Property<Guid>(role, "Id"))
            .SingleAsync();
    }

    private async Task<bool> HasRoleAsync(string name)
    {
        await using var context = _factory.CreateContext();
        return await context.Roles.AnyAsync(role => role.Name == name);
    }

    private async Task<bool> IsDeveloperAsync(Guid roleId)
    {
        await using var context = _factory.CreateContext();
        return (await context.Roles.SingleAsync(role => EF.Property<Guid>(role, "Id") == roleId)).IsDeveloper;
    }

    private async Task<bool> HasGrantAsync(Guid roleId)
    {
        await using var context = _factory.CreateContext();
        return await context.Set<Dictionary<string, object>>("RoleGrants")
            .AnyAsync(row => EF.Property<Guid>(row, "RoleId") == roleId);
    }
}
