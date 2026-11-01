using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Tests.Helpers;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class UserManagementQueriesTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<Guid> SeedUserAsync(string email, string displayName = "")
    {
        await using var ctx = _factory.CreateContext();
        var id = await DatabaseSeedHelper.SeedHumanUserAsync(ctx, email);
        await new UserCommands(ctx).ChangeDisplayName(id, displayName);
        return id;
    }

    private async Task AssignAsync(Guid userId, BuiltInRole role)
    {
        await using var ctx = _factory.CreateContext();
        await new RoleCommands(ctx).AssignBuiltIn(userId, role);
    }

    private async Task<PagedResult<IUserManagementQueries.ManagedUser>> ListAsync(
        Option<string> search = default,
        Flipper? flipper = null)
    {
        await using var ctx = _factory.CreateContext();
        return await new UserManagementQueries(ctx).ListUsers(flipper ?? new Flipper(1, 50), search, Now);
    }

    private static IReadOnlyList<IUserManagementQueries.ManagedUser> Items(
        PagedResult<IUserManagementQueries.ManagedUser> page) =>
        page.Results.Match(() => (IReadOnlyList<IUserManagementQueries.ManagedUser>)[], items => items.ToList());

    [Fact]
    public async Task ListUsers_ReturnsEmailsRolesAndBanOrderedByDisplayName()
    {
        var zed = await SeedUserAsync("zed@test.com", "Zed");
        var ada = await SeedUserAsync("ada@test.com", "Ada");
        await AssignAsync(ada, BuiltInAccess.Administrators);
        await AssignAsync(ada, BuiltInAccess.Developers);

        await using (var ctx = _factory.CreateAuditedContext(AuditActor.ForUser(ada)))
        {
            await new WallOfShameCommands(ctx).Ban(zed, ada, new Reason("rude"));
        }

        var users = Items(await ListAsync());

        Assert.Equal(["Ada", "Zed"], users.Select(user => user.DisplayName));
        Assert.Equal(["ada@test.com"], users[0].Emails);
        Assert.Equal(
            [DomainConstants.AdministratorsRole, DomainConstants.DevelopersRole],
            users[0].Roles);
        Assert.Equal(None, users[0].Ban);
        Assert.Equal(Some(new IWallOfShameQueries.ActiveBan("rude", None)), users[1].Ban);
    }

    [Fact]
    public async Task ListUsers_PagesAndCountsEveryUser()
    {
        foreach (var name in new[] { "A", "B", "C" })
        {
            await SeedUserAsync($"{name.ToLowerInvariant()}@test.com", name);
        }

        var second = await ListAsync(flipper: new Flipper(2, 2));

        Assert.Equal((3L, 2L, 2L, 2), (second.Total, second.Page, second.PageCount, second.PageSize));
        Assert.Equal(["C"], Items(second).Select(user => user.DisplayName));
    }

    [Theory]
    [InlineData("LOVE", "Ada Lovelace")]
    [InlineData("grace@", "Grace")]
    public async Task ListUsers_SearchMatchesDisplayNameOrEmailIgnoringCase(string term, string expected)
    {
        await SeedUserAsync("ada@test.com", "Ada Lovelace");
        await SeedUserAsync("grace@navy.mil", "Grace");

        var users = Items(await ListAsync(Some(term)));

        Assert.Equal([expected], users.Select(user => user.DisplayName));
    }

    [Fact]
    public async Task ListUsers_BlankSearch_ReturnsEverybody()
    {
        await SeedUserAsync("a@test.com", "A");
        await SeedUserAsync("b@test.com", "B");

        Assert.Equal(2, Items(await ListAsync(Some("   "))).Count);
    }

    [Fact]
    public async Task ListUsers_WithoutUsers_ReturnsNoResults()
    {
        var page = await ListAsync();

        Assert.Equal(0, page.Total);
        Assert.Equal(None, page.Results);
    }

    [Fact]
    public async Task ListUsers_AndTryFindUser_ExcludeTheBuiltInSystemAndGuestUsers()
    {
        await using (var seed = _factory.CreateContext())
        {
            await DatabaseSeedHelper.EnsureBuiltInUsersAsync(seed);
        }

        await SeedUserAsync("real@test.com", "Real");

        await using var ctx = _factory.CreateContext();
        var queries = new UserManagementQueries(ctx);
        var users = Items(await queries.ListUsers(new Flipper(1, 50), None, Now));

        Assert.Equal(["Real"], users.Select(user => user.DisplayName));
        Assert.Equal(None, await queries.TryFindUser(DomainConstants.SystemUserId, Now));
        Assert.Equal(None, await queries.TryFindUser(DomainConstants.GuestUserId, Now));
    }

    [Fact]
    public async Task TryFindUser_ReturnsTheUserOrNone()
    {
        var id = await SeedUserAsync("find@test.com", "Finder");

        await using var ctx = _factory.CreateContext();
        var queries = new UserManagementQueries(ctx);

        var found = await queries.TryFindUser(id, Now);
        Assert.Equal("Finder", found.Match(() => "", user => user.DisplayName));
        Assert.Equal(None, await queries.TryFindUser(Guid.NewGuid(), Now));
    }
}
