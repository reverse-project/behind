using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Tests.Helpers;

namespace Behind.Tests.CoreDatabase.Repository;

public sealed class RoleCommandsRevokeTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<IReadOnlyList<string>> RoleNamesAsync(Guid userId)
    {
        await using var ctx = _factory.CreateContext();
        var roles = await new RoleQueries(ctx).GetRolesByUserId(userId);
        return roles.Select(role => role.Name).Order().ToList();
    }

    [Fact]
    public async Task RevokeBuiltIn_RemovesOnlyTheRevokedRole()
    {
        Guid userId;
        await using (var seed = _factory.CreateContext())
        {
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "revoke@test.com");
            var commands = new RoleCommands(seed);
            await commands.AssignBuiltIn(userId, BuiltInAccess.Administrators);
            await commands.AssignBuiltIn(userId, BuiltInAccess.Developers);
        }

        await using (var ctx = _factory.CreateContext())
        {
            await new RoleCommands(ctx).RevokeBuiltIn(userId, BuiltInAccess.Administrators);
        }

        Assert.Equal([DomainConstants.DevelopersRole], await RoleNamesAsync(userId));
    }

    [Fact]
    public async Task RevokeBuiltIn_WithoutTheRole_DoesNothing()
    {
        Guid userId;
        await using (var seed = _factory.CreateContext())
        {
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "none@test.com");
            await new RoleCommands(seed).AssignBuiltIn(userId, BuiltInAccess.Users);
        }

        await using (var ctx = _factory.CreateContext())
        {
            await new RoleCommands(ctx).RevokeBuiltIn(userId, BuiltInAccess.Developers);
        }

        Assert.Equal([DomainConstants.UsersRole], await RoleNamesAsync(userId));
    }

    [Fact]
    public async Task RevokeBuiltIn_ThenAssignAgain_RestoresTheRole()
    {
        Guid userId;
        await using (var seed = _factory.CreateContext())
        {
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "again@test.com");
            await new RoleCommands(seed).AssignBuiltIn(userId, BuiltInAccess.Administrators);
        }

        await using (var ctx = _factory.CreateContext())
        {
            var commands = new RoleCommands(ctx);
            await commands.RevokeBuiltIn(userId, BuiltInAccess.Administrators);
            await commands.AssignBuiltIn(userId, BuiltInAccess.Administrators);
        }

        Assert.Equal([DomainConstants.AdministratorsRole], await RoleNamesAsync(userId));
    }
}
