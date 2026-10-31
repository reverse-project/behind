using Behind.Core.Database;
using Behind.Core.Database.Queries;
using Behind.Core.Database.Sqlite;
using Behind.Tests.Helpers;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class UserQueriesGrantTests : IDisposable
{
    private const string Email = "granted@test.com";

    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<(Guid UserId, Guid RoleId, Guid GrantId)> SeedGrantedUserAsync(string grant = "ViewDeleted")
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, Email);
        var roleId = await DatabaseSeedHelper.SeedUserRoleAsync(seed, "admin");
        var grantId = await DatabaseSeedHelper.SeedGrantAsync(seed, grant);
        await DatabaseSeedHelper.AssignRoleToUserAsync(seed, userId, roleId);
        await DatabaseSeedHelper.AssignGrantToRoleAsync(seed, roleId, grantId);
        return (userId, roleId, grantId);
    }

    private async Task<bool> HasGrantAsync(string email = Email, string grant = "ViewDeleted")
    {
        await using var ctx = _factory.CreateContext();
        return await new UserQueries(ctx).HasGrant(email, grant);
    }

    [Fact]
    public async Task HasGrant_UserWithGrantThroughRole_ReturnsTrue()
    {
        await SeedGrantedUserAsync();

        Assert.True(await HasGrantAsync());
    }

    [Fact]
    public async Task HasGrant_EmailCaseDiffers_ReturnsTrue()
    {
        await SeedGrantedUserAsync();

        Assert.True(await HasGrantAsync("Granted@Test.com"));
    }

    [Fact]
    public async Task HasGrant_OtherGrant_ReturnsFalse()
    {
        await SeedGrantedUserAsync("read");

        Assert.False(await HasGrantAsync());
    }

    [Fact]
    public async Task HasGrant_UnknownEmail_ReturnsFalse()
    {
        await SeedGrantedUserAsync();

        Assert.False(await HasGrantAsync("nobody@test.com"));
    }

    [Fact]
    public async Task HasGrant_UserWithoutRoles_ReturnsFalse()
    {
        await using var seed = _factory.CreateContext();
        await DatabaseSeedHelper.SeedHumanUserAsync(seed, Email);

        Assert.False(await HasGrantAsync());
    }

    [Fact]
    public async Task HasGrant_DeletedUser_ReturnsFalse()
    {
        var (userId, _, _) = await SeedGrantedUserAsync();
        await using (var ctx = _factory.CreateContext())
        {
            await ctx.GetRepository<HumanUser, Guid>().DeleteAsync(userId);
            await ctx.SaveChangesAsync();
        }

        Assert.False(await HasGrantAsync());
    }

    [Fact]
    public async Task HasGrant_DeletedRole_ReturnsFalse()
    {
        var (_, roleId, _) = await SeedGrantedUserAsync();
        await using (var ctx = _factory.CreateContext())
        {
            await ctx.GetRepository<UserRole, Guid>().DeleteAsync(roleId);
            await ctx.SaveChangesAsync();
        }

        Assert.False(await HasGrantAsync());
    }

    [Fact]
    public async Task HasGrant_DeletedGrant_ReturnsFalse()
    {
        var (_, _, grantId) = await SeedGrantedUserAsync();
        await using (var ctx = _factory.CreateContext())
        {
            await ctx.GetRepository<Grant, Guid>().DeleteAsync(grantId);
            await ctx.SaveChangesAsync();
        }

        Assert.False(await HasGrantAsync());
    }
}
