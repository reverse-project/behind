using Behind.Common.Database;
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
        Flipper? flipper = null,
        Option<string> letter = default)
    {
        await using var ctx = _factory.CreateContext();
        return await new UserManagementQueries(ctx).ListUsers(flipper ?? new Flipper(1, 50), search, letter, DeletionState.Active, Now);
    }

    private async Task<IReadOnlyList<string>> LettersAsync(Option<string> search = default)
    {
        await using var ctx = _factory.CreateContext();
        return await new UserManagementQueries(ctx).GetLetters(search, DeletionState.Active);
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
    public async Task ListUsers_ReportsWhetherThePrimaryEmailIsConfirmed()
    {
        var ada = await SeedUserAsync("ada@test.com", "Ada");
        await SeedUserAsync("zed@test.com", "Zed");

        await using (var ctx = _factory.CreateAuditedContext(AuditActor.ForUser(ada)))
        {
            await new EmailCommands(ctx, new FixedTimeProvider(Now)).ConfirmPrimary(ada);
        }

        var users = Items(await ListAsync());

        Assert.Equal([true, false], users.Select(user => user.EmailConfirmed));
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

    [Theory]
    [InlineData("a", new[] { "Ada", "adam@test.com" })]
    [InlineData("B", new[] { "Bob" })]
    [InlineData("#", new[] { "42@test.com", "Émile" })]
    [InlineData("Z", new string[0])]
    public async Task ListUsers_LetterFiltersByTheFirstLetterOfTheShownLabel(string letter, string[] expected)
    {
        await SeedUserAsync("ada@test.com", "Ada");
        await SeedUserAsync("adam@test.com");
        await SeedUserAsync("bob@test.com", "Bob");
        await SeedUserAsync("42@test.com");
        await SeedUserAsync("emile@test.com", "Émile");

        var users = Items(await ListAsync(letter: Some(letter)));

        Assert.Equal(expected.Order(), users.Select(user => user.DisplayName.Length > 0 ? user.DisplayName : user.Emails[0]).Order());
    }

    [Fact]
    public async Task ListUsers_OrdersByTheShownLabelIgnoringCase()
    {
        await SeedUserAsync("zed@test.com", "zed");
        await SeedUserAsync("bob@test.com", "Bob");
        await SeedUserAsync("amy@test.com");

        var users = Items(await ListAsync());

        Assert.Equal(["amy@test.com", "bob@test.com", "zed@test.com"], users.Select(user => user.Emails[0]));
    }

    [Fact]
    public async Task ListUsers_LetterCombinesWithSearchAndCountsOnlyTheBucket()
    {
        await SeedUserAsync("ann@test.com", "Ann");
        await SeedUserAsync("alf@other.com", "Alf");
        await SeedUserAsync("bob@test.com", "Bob");

        var page = await ListAsync(Some("test"), letter: Some("A"));

        Assert.Equal(1, page.Total);
        Assert.Equal(["Ann"], Items(page).Select(user => user.DisplayName));
    }

    [Fact]
    public async Task GetLetters_ReturnsOnlyPresentBucketsInAlphabeticalOrderWithOtherLast()
    {
        await SeedUserAsync("c@test.com", "Cleo");
        await SeedUserAsync("a@test.com", "ann");
        await SeedUserAsync("9@test.com");
        await SeedUserAsync("e@test.com", "Émile");

        Assert.Equal(["A", "C", "#"], await LettersAsync());
    }

    [Fact]
    public async Task GetLetters_RespectsTheSearchTerm()
    {
        await SeedUserAsync("c@test.com", "Cleo");
        await SeedUserAsync("a@test.com", "Ann");

        Assert.Equal(["C"], await LettersAsync(Some("cleo")));
    }

    [Fact]
    public async Task GetLetters_WithoutUsers_IsEmpty()
    {
        Assert.Empty(await LettersAsync());
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
        var users = Items(await queries.ListUsers(new Flipper(1, 50), None, None, DeletionState.Active, Now));

        Assert.Equal(["Real"], users.Select(user => user.DisplayName));
        Assert.Equal(None, await queries.TryFindUser(DomainConstants.SystemUserId, DeletionState.Active, Now));
        Assert.Equal(None, await queries.TryFindUser(DomainConstants.GuestUserId, DeletionState.Active, Now));
    }

    [Fact]
    public async Task TryFindUser_ReturnsTheUserOrNone()
    {
        var id = await SeedUserAsync("find@test.com", "Finder");

        await using var ctx = _factory.CreateContext();
        var queries = new UserManagementQueries(ctx);

        var found = await queries.TryFindUser(id, DeletionState.Active, Now);
        Assert.Equal("Finder", found.Match(() => "", user => user.DisplayName));
        Assert.Equal(None, await queries.TryFindUser(Guid.NewGuid(), DeletionState.Active, Now));
    }

    private async Task DeleteAsync(Guid userId)
    {
        await using var ctx = _factory.CreateAuditedContext(AuditActor.Anonymous);
        await new UserCommands(ctx).Delete(userId);
    }

    [Fact]
    public async Task ListUsers_ActiveState_HidesDeletedUsers_EvenWhenDeletedRecordsAreIncluded()
    {
        await SeedUserAsync("kept@test.com", "Kept");
        var removed = await SeedUserAsync("removed@test.com", "Removed");
        await DeleteAsync(removed);

        await using var ctx = _factory.CreateContext();
        using var scope = ctx.SoftDelete.IncludeDeleted();
        var users = Items(await new UserManagementQueries(ctx)
            .ListUsers(new Flipper(1, 50), None, None, DeletionState.Active, Now));

        Assert.Equal(["Kept"], users.Select(user => user.DisplayName));
    }

    [Fact]
    public async Task ListUsers_DeletedState_ReturnsOnlyDeletedUsersWithTheirEmails()
    {
        await SeedUserAsync("kept@test.com", "Kept");
        var removed = await SeedUserAsync("removed@test.com", "Removed");
        await DeleteAsync(removed);

        await using var ctx = _factory.CreateContext();
        var users = Items(await new UserManagementQueries(ctx)
            .ListUsers(new Flipper(1, 50), None, None, DeletionState.Deleted, Now));

        var user = Assert.Single(users);
        Assert.Equal("Removed", user.DisplayName);
        Assert.Equal(["removed@test.com"], user.Emails);
        Assert.Equal(DeletionState.Deleted, user.State);
    }

    [Fact]
    public async Task ListUsers_DeletedState_DoesNotLeakTheScopeToLaterQueries()
    {
        var removed = await SeedUserAsync("removed@test.com", "Removed");
        await DeleteAsync(removed);

        await using var ctx = _factory.CreateContext();
        var queries = new UserManagementQueries(ctx);
        await queries.ListUsers(new Flipper(1, 50), None, None, DeletionState.Deleted, Now);

        Assert.False(ctx.SoftDelete.IncludesDeleted);
    }

    [Fact]
    public async Task ListUsers_RestoredUser_IsActiveAgain()
    {
        var id = await SeedUserAsync("back@test.com", "Back");
        await DeleteAsync(id);

        await using (var ctx = _factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            await new UserCommands(ctx).Restore(id);
        }

        await using var verify = _factory.CreateContext();
        var queries = new UserManagementQueries(verify);
        Assert.Equal(["Back"], Items(await queries.ListUsers(new Flipper(1, 50), None, None, DeletionState.Active, Now))
            .Select(user => user.DisplayName));
        Assert.Empty(Items(await queries.ListUsers(new Flipper(1, 50), None, None, DeletionState.Deleted, Now)));
    }

    [Fact]
    public async Task GetLetters_DeletedState_ListsOnlyDeletedUsers()
    {
        await SeedUserAsync("alpha@test.com", "Alpha");
        var removed = await SeedUserAsync("zulu@test.com", "Zulu");
        await DeleteAsync(removed);

        await using var ctx = _factory.CreateContext();
        var queries = new UserManagementQueries(ctx);

        Assert.Equal(["Z"], await queries.GetLetters(None, DeletionState.Deleted));
        Assert.Equal(["A"], await queries.GetLetters(None, DeletionState.Active));
    }

    [Fact]
    public async Task TryFindUser_RespectsTheRequestedState()
    {
        var id = await SeedUserAsync("state@test.com", "State");
        await DeleteAsync(id);

        await using var ctx = _factory.CreateContext();
        var queries = new UserManagementQueries(ctx);

        Assert.Equal(None, await queries.TryFindUser(id, DeletionState.Active, Now));
        var found = await queries.TryFindUser(id, DeletionState.Deleted, Now);
        Assert.Equal(DeletionState.Deleted, found.Match(() => DeletionState.Active, user => user.State));
    }
}
