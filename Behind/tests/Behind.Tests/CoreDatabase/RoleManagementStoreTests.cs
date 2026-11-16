using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Tests.Helpers;

namespace Behind.Tests.CoreDatabase;

public sealed class RoleManagementStoreTests : IDisposable
{
    private static readonly Flipper FirstPage = new(1, 10);

    private readonly TestDbContextFactory _factory = new();
    private readonly AuditActor _actor;

    public RoleManagementStoreTests()
    {
        using var seed = _factory.CreateContext();
        var actorId = DatabaseSeedHelper.SeedHumanUserAsync(seed, "actor@test.com").GetAwaiter().GetResult();
        DatabaseSeedHelper.EnsureBuiltInUsersAsync(seed).GetAwaiter().GetResult();
        _actor = AuditActor.ForUser(actorId);
    }

    public void Dispose() => _factory.Dispose();

    private async Task<Guid> SeedAsync(string name)
    {
        await using var ctx = _factory.CreateContext();
        return await DatabaseSeedHelper.SeedUserRoleAsync(ctx, name);
    }

    private async Task<T> WithCommandsAsync<T>(Func<RoleCommands, Task<T>> action)
    {
        await using var ctx = _factory.CreateAuditedContext(_actor);
        return await action(new RoleCommands(ctx, AccessCatalog.Core));
    }

    private async Task<IReadOnlyList<string>> NamesAsync(
        Option<string>? search = null,
        Option<string>? letter = null,
        DeletionState state = DeletionState.Active,
        Flipper? flipper = null)
    {
        await using var ctx = _factory.CreateContext();
        var page = await new RoleQueries(ctx).PageRoles(flipper ?? FirstPage, search ?? None, letter ?? None, state);

        return page.Results.Match(() => (IReadOnlyList<string>)[], roles => roles.Select(role => role.Role.Name).ToList());
    }

    [Fact]
    public async Task PageRoles_ListsRolesAlphabeticallyAndPagesThem()
    {
        foreach (var name in new[] { "Charlie", "alpha", "Bravo", "Delta" })
        {
            await SeedAsync(name);
        }

        await using var ctx = _factory.CreateContext();
        var page = await new RoleQueries(ctx).PageRoles(new Flipper(2, 3), None, None, DeletionState.Active);

        Assert.Equal(4, page.Total);
        Assert.Equal(2, page.PageCount);
        Assert.Equal(["Delta"], await NamesAsync(flipper: new Flipper(2, 3)));
        Assert.Equal(["alpha", "Bravo", "Charlie"], await NamesAsync(flipper: new Flipper(1, 3)));
    }

    [Fact]
    public async Task PageRoles_SearchesTheNameIgnoringCase()
    {
        await SeedAsync("Editors");
        await SeedAsync("Support");

        Assert.Equal(["Editors"], await NamesAsync(search: Some("EDIT")));
        Assert.Empty(await NamesAsync(search: Some("nothing")));
    }

    [Fact]
    public async Task PageRoles_FiltersByLetterAndListsTheLetters()
    {
        await SeedAsync("Editors");
        await SeedAsync("Support");
        await SeedAsync("9 lives");

        await using var ctx = _factory.CreateContext();

        Assert.Equal(["Support"], await NamesAsync(letter: Some("S")));
        Assert.Equal(["9 lives"], await NamesAsync(letter: Some("#")));
        Assert.Equal(["E", "S", "#"], await new RoleQueries(ctx).GetLetters(None, DeletionState.Active));
    }

    [Fact]
    public async Task PageRoles_SeparatesActiveFromDeletedRoles()
    {
        var kept = await SeedAsync("Kept");
        var gone = await SeedAsync("Gone");
        await WithCommandsAsync(async commands =>
        {
            await commands.Delete(gone);
            return true;
        });

        Assert.Equal(["Kept"], await NamesAsync());
        Assert.Equal(["Gone"], await NamesAsync(state: DeletionState.Deleted));

        await using var ctx = _factory.CreateContext();
        Assert.Equal(["G"], await new RoleQueries(ctx).GetLetters(None, DeletionState.Deleted));
        Assert.NotEqual(kept, gone);
    }

    [Fact]
    public async Task Restore_DeletedRole_ReturnsItToTheActiveList()
    {
        var id = await SeedAsync("Gone");
        await WithCommandsAsync(async commands =>
        {
            await commands.Delete(id);
            return true;
        });

        Assert.True(await WithCommandsAsync(commands => commands.Restore(id)));

        Assert.Equal(["Gone"], await NamesAsync());
        Assert.Empty(await NamesAsync(state: DeletionState.Deleted));
    }

    [Fact]
    public async Task Restore_ActiveOrUnknownRole_ReturnsFalse()
    {
        var id = await SeedAsync("Kept");

        Assert.False(await WithCommandsAsync(commands => commands.Restore(id)));
        Assert.False(await WithCommandsAsync(commands => commands.Restore(Guid.NewGuid())));
    }

    [Fact]
    public async Task HardDelete_DeletedRole_RemovesItForGood()
    {
        var id = await SeedAsync("Gone");
        await WithCommandsAsync(async commands =>
        {
            await commands.Delete(id);
            return true;
        });

        Assert.Equal(HardDeleteResult.Deleted, await WithCommandsAsync(commands => commands.HardDelete(id)));

        Assert.Empty(await NamesAsync(state: DeletionState.Deleted));
        Assert.False(await WithCommandsAsync(commands => commands.Restore(id)));
    }

    [Fact]
    public async Task HardDelete_ActiveOrUnknownRole_ReturnsNotFound()
    {
        var id = await SeedAsync("Kept");

        Assert.Equal(HardDeleteResult.NotFound, await WithCommandsAsync(commands => commands.HardDelete(id)));
        Assert.Equal(HardDeleteResult.NotFound, await WithCommandsAsync(commands => commands.HardDelete(Guid.NewGuid())));
        Assert.Equal(["Kept"], await NamesAsync());
    }
}
