using Behind.Common.Database;
using Behind.Common.Database.Raw;
using Behind.Core.Database;
using Behind.Core.Database.Queries;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class ModuleContextTests : IDisposable
{
    private readonly NotesTestBed _bed = new();
    private readonly TestDbContextFactory _core = new();

    public void Dispose()
    {
        _bed.Dispose();
        _core.Dispose();
    }

    private static async Task<IReadOnlyList<string>> GrantNamesOf(AbstractCoreContext context, string role) =>
        (await new RoleQueries(context).GetRoleByName(role)).Match(
            () => throw new InvalidOperationException("The role was not seeded."),
            found => found.Role.GrantNames);

    [Fact]
    public void Model_ContainsTheEntitiesOfTheModuleDatabase()
    {
        using var context = _bed.CreateContext();

        Assert.NotNull(context.Model.FindEntityType(typeof(Note)));
        Assert.NotNull(context.Model.FindEntityType(typeof(NoteLog)));
        Assert.NotNull(context.Model.FindEntityType(typeof(User)));
    }

    [Fact]
    public void Model_OfTheCoreContextHasNoModuleEntities()
    {
        using var context = _core.CreateContext();

        Assert.Null(context.Model.FindEntityType(typeof(Note)));
        Assert.Empty(context.Modules.Modules);
    }

    [Fact]
    public void Model_AppliesTheSoftDeleteOfTheModuleEntities()
    {
        using var context = _bed.CreateContext();

        Assert.True(context.Model.FindEntityType(typeof(Note))!.IsSoftDeletable);
        Assert.False(context.Model.FindEntityType(typeof(NoteLog))!.IsSoftDeletable);
        Assert.True(context.Model.FindEntityType(typeof(Site))!.IsSoftDeletable);
    }

    [Fact]
    public async Task ModuleEntities_CanBeSavedAndQueriedThroughTheModuleContextInterface()
    {
        await using (var write = _bed.CreateContext())
        {
            INotesContext notes = write;
            notes.Notes.Add(new Note { Id = Guid.NewGuid(), Title = "First" });
            await write.SaveChangesAsync();
        }

        await using var read = _bed.CreateContext();

        Assert.Equal(["First"], await ((INotesContext)read).Notes.Select(note => note.Title).ToListAsync());
    }

    [Fact]
    public void RawTables_ListTheModuleTablesAndMarkTheReadOnlyOnes()
    {
        using var context = _bed.CreateContext();
        var tables = context.RawTables.ToDictionary(table => table.EntityName);

        Assert.False(context.IsReadOnly(tables[nameof(Note)]));
        Assert.True(context.IsReadOnly(tables[nameof(NoteLog)]));
        Assert.True(context.IsReadOnly(tables[nameof(AuditLogEntry)]));
        Assert.False(context.IsReadOnly(tables[nameof(Grant)]));
    }

    [Fact]
    public void IsReadOnly_IgnoresTheModuleEntitiesOfTheCoreContext()
    {
        using var context = _core.CreateContext();

        Assert.False(context.IsReadOnly(new RawTable("NoteLogs", null, nameof(NoteLog), [])));
    }

    [Fact]
    public async Task AuditLabels_IncludeTheLabelsOfTheModuleSources()
    {
        var id = Guid.NewGuid();
        await using var context = _bed.CreateContext();
        context.Notes.Add(new Note { Id = id, Title = "Shopping" });
        await context.SaveChangesAsync();

        var labels = await context.ResolveAuditLabelsAsync([id], [new NotesAuditLabels(context)]);

        Assert.Equal("Shopping", labels.Label(id.ToString()));
    }

    [Fact]
    public async Task AuditLabels_LeaveUnknownIdsUntouchedWithoutSources()
    {
        var id = Guid.NewGuid();
        await using var context = _bed.CreateContext();

        var labels = await context.ResolveAuditLabelsAsync([id], []);

        Assert.Equal(id.ToString(), labels.Label(id.ToString()));
    }

    [Fact]
    public async Task EnsureBuiltInRoles_SeedsTheGrantsOfTheModulesIntoTheBuiltInRoles()
    {
        var access = AccessCatalog.From(NotesModule.Catalog).GetOrElse(AccessCatalog.Core);
        await using var context = _bed.CreateContext();

        await context.EnsureBuiltInRolesAsync(access);

        var grants = await context.Grants.Select(grant => grant.Name).ToListAsync();
        Assert.All(["Notes.Read", "Notes.Write", "Notes.Delete", "Notes.Manage"], name => Assert.Contains(name, grants));
        var users = await GrantNamesOf(context, DomainConstants.UsersRole);
        Assert.Contains("Notes.Read", users);
        Assert.DoesNotContain("Notes.Write", users);
        var administrators = await GrantNamesOf(context, DomainConstants.AdministratorsRole);
        Assert.Contains("Notes.Manage", administrators);
    }

    [Fact]
    public async Task EnsureBuiltInRoles_AddsModuleGrantsToExistingRolesWithoutLosingCustomisations()
    {
        var access = AccessCatalog.From(NotesModule.Catalog).GetOrElse(AccessCatalog.Core);
        await using var context = _bed.CreateContext();
        await context.EnsureBuiltInRolesAsync(AccessCatalog.Core);
        var userRead = BuiltInAccess.Grants.Single(grant => grant.Grant.Name == "User.Read");
        var roleGrants = context.Set<Dictionary<string, object>>("RoleGrants");
        roleGrants.RemoveRange(await roleGrants
            .Where(roleGrant => EF.Property<Guid>(roleGrant, "RoleId") == BuiltInAccess.Users.Id
                                && EF.Property<Guid>(roleGrant, "GrantId") == userRead.Id)
            .ToListAsync());
        await context.SaveChangesAsync();

        await context.EnsureBuiltInRolesAsync(access);

        var users = await GrantNamesOf(context, DomainConstants.UsersRole);
        Assert.Contains("Notes.Read", users);
        Assert.DoesNotContain("User.Read", users);
    }

    [Fact]
    public async Task EnsureBuiltInRoles_IsIdempotentForModuleGrants()
    {
        var access = AccessCatalog.From(NotesModule.Catalog).GetOrElse(AccessCatalog.Core);
        await using var context = _bed.CreateContext();

        await context.EnsureBuiltInRolesAsync(access);
        var grants = await context.Grants.CountAsync();
        var roleGrants = await context.Set<Dictionary<string, object>>("RoleGrants").CountAsync();
        await context.EnsureBuiltInRolesAsync(access);

        Assert.Equal(grants, await context.Grants.CountAsync());
        Assert.Equal(roleGrants, await context.Set<Dictionary<string, object>>("RoleGrants").CountAsync());
        Assert.Equal(access.Roles.Sum(role => role.Grants.Count), roleGrants);
    }

    [Fact]
    public async Task EnsureBuiltInMenus_AddsTheMenuItemsOfTheModulesToTheMainMenu()
    {
        await using var context = _bed.CreateContext();
        await context.EnsureBuiltInRolesAsync(AccessCatalog.Core);

        await context.EnsureBuiltInMenusAsync(NotesModule.Catalog);
        await context.EnsureBuiltInMenusAsync(NotesModule.Catalog);

        var items = await context.MenuItems.Select(item => new { item.Title, item.Url, item.Kind }).ToListAsync();
        Assert.Single(items, item => item.Url == "#/notes" && item.Kind == MenuItemKind.Link);
        Assert.Single(items, item => item.Url == "#/manage/notes" && item.Kind == MenuItemKind.User);
        Assert.Equal(BuiltInMenus.Main.Items.Count + 2, items.Count);
    }

    [Fact]
    public async Task EnsureBuiltInMenus_WithoutModulesAddsNoModuleItems()
    {
        await using var context = _bed.CreateContext();
        await context.EnsureBuiltInRolesAsync(AccessCatalog.Core);

        await context.EnsureBuiltInMenusAsync(ModuleCatalog.Empty);

        Assert.Equal(BuiltInMenus.Main.Items.Count, await context.MenuItems.CountAsync());
    }
}
