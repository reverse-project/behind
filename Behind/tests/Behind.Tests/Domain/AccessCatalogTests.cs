namespace Behind.Tests.Domain;

public class AccessCatalogTests
{
    private static AccessCatalog With(params ModuleDescriptor[] modules) =>
        AccessCatalog.From(ModuleCatalog.Create(modules).GetOrElse(ModuleCatalog.Empty))
            .GetOrElse(AccessCatalog.Core);

    private static ModuleDescriptor Module(string key, string area, ModuleAudience audience) =>
        new(key, key) { GrantAreas = [new ModuleGrantArea(area, audience)] };

    private static IReadOnlyList<string> Grants(AccessCatalog catalog, Guid roleId) =>
        catalog.FindRole(roleId).Match(() => [], role => role.Grants.Select(grant => grant.Grant.Name).ToList());

    [Fact]
    public void Core_MatchesTheBuiltInAccess()
    {
        Assert.Equal(BuiltInAccess.Grants, AccessCatalog.Core.Grants);
        Assert.Equal(BuiltInAccess.Roles, AccessCatalog.Core.Roles);
        Assert.Equal(BuiltInAccess.GrantNames, AccessCatalog.Core.GrantNames);
        Assert.Equal(BuiltInAccess.Restrictions, AccessCatalog.Core.Restrictions);
    }

    [Fact]
    public void From_WithoutModulesEqualsTheCoreAccess()
    {
        var catalog = With();

        Assert.Equal(AccessCatalog.Core.GrantNames, catalog.GrantNames);
        Assert.Equal(AccessCatalog.Core.Roles.Select(role => role.Grants.Count), catalog.Roles.Select(role => role.Grants.Count));
    }

    [Fact]
    public void From_AddsEveryActionOfTheModuleAreas()
    {
        var catalog = With(NotesModule.Descriptor);

        Assert.All(
            ["Notes.Read", "Notes.Write", "Notes.Delete", "Notes.Manage"],
            name => Assert.True(catalog.IsKnownGrant(name)));
        Assert.False(AccessCatalog.Core.IsKnownGrant("Notes.Read"));
        Assert.Equal(AccessCatalog.Core.Grants.Count + 4, catalog.Grants.Count);
        Assert.Equal(catalog.Grants.Count, catalog.Restrictions.Count);
    }

    [Fact]
    public void From_GivesModuleGrantsStableUniqueIds()
    {
        var first = With(NotesModule.Descriptor);
        var second = With(NotesModule.Descriptor);

        Assert.Equal(first.Grants.Select(grant => grant.Id), second.Grants.Select(grant => grant.Id));
        Assert.Equal(first.Grants.Count, first.Grants.Select(grant => grant.Id).Distinct().Count());
        Assert.Equal(first.Restrictions.Count, first.Restrictions.Select(restriction => restriction.Id).Distinct().Count());
        Assert.Empty(first.Restrictions.Select(restriction => restriction.Id).Intersect(first.Grants.Select(grant => grant.Id)));
    }

    [Fact]
    public void From_EveryoneAreasGrantAdministratorsEverythingAndUsersRead()
    {
        var catalog = With(Module("notes", "Notes", ModuleAudience.Everyone));

        Assert.Contains("Notes.Manage", Grants(catalog, BuiltInAccess.Administrators.Id));
        Assert.Contains("Notes.Read", Grants(catalog, BuiltInAccess.Users.Id));
        Assert.DoesNotContain("Notes.Write", Grants(catalog, BuiltInAccess.Users.Id));
        Assert.DoesNotContain("Notes.Read", Grants(catalog, BuiltInAccess.Developers.Id));
    }

    [Fact]
    public void From_AdministrationAreasStayAwayFromUsers()
    {
        var catalog = With(Module("tools", "Tools", ModuleAudience.Administration));

        Assert.Contains("Tools.Read", Grants(catalog, BuiltInAccess.Administrators.Id));
        Assert.DoesNotContain("Tools.Read", Grants(catalog, BuiltInAccess.Users.Id));
        Assert.DoesNotContain("Tools.Read", Grants(catalog, BuiltInAccess.Developers.Id));
        Assert.False(catalog.IsDeveloperGrant("Tools.Read"));
    }

    [Fact]
    public void From_DeveloperAreasGoToDevelopersOnly()
    {
        var catalog = With(Module("internals", "Internals", ModuleAudience.Developer));

        Assert.Equal(
            ["Internals.Read", "Internals.Manage"],
            Grants(catalog, BuiltInAccess.Developers.Id).Where(name => name.StartsWith("Internals.")));
        Assert.DoesNotContain("Internals.Read", Grants(catalog, BuiltInAccess.Administrators.Id));
        Assert.DoesNotContain("Internals.Read", Grants(catalog, BuiltInAccess.Users.Id));
        Assert.All(
            ["Internals.Read", "Internals.Write", "Internals.Delete", "Internals.Manage"],
            name => Assert.True(catalog.IsDeveloperGrant(name)));
    }

    [Fact]
    public void From_KeepsTheRoleIdentityAndTheRoleGrantsInSync()
    {
        var catalog = With(NotesModule.Descriptor);
        var role = catalog.FindRole(BuiltInAccess.Administrators.Id).Match(
            () => throw new InvalidOperationException(),
            found => found);

        Assert.Equal(BuiltInAccess.Administrators.Id, role.Id);
        Assert.Equal(role.Grants.Select(grant => grant.Grant.Name), role.Role.GrantNames);
        Assert.True(catalog.IsBuiltIn(role.Id));
        Assert.False(catalog.IsBuiltIn(Guid.NewGuid()));
    }

    [Fact]
    public void From_ModuleGrantsAreSelectableAndValid()
    {
        var catalog = With(NotesModule.Descriptor);

        Assert.True(catalog.IsValid(new AccessSelection(["Notes.Read"], ["Notes.Write"])));
        Assert.False(AccessCatalog.Core.IsValid(new AccessSelection(["Notes.Read"], [])));
    }

    [Fact]
    public void GrantsDeveloperAccess_RecognisesModuleDeveloperGrants()
    {
        var catalog = With(Module("internals", "Internals", ModuleAudience.Developer));

        Assert.True(catalog.GrantsDeveloperAccess(new AccessSelection(["Internals.Read"], [])));
        Assert.False(AccessCatalog.Core.GrantsDeveloperAccess(new AccessSelection(["Internals.Read"], [])));
    }

    [Fact]
    public void From_RejectsAreasAlreadyUsedByTheCore()
    {
        var module = Module("clash", GrantArea.Site, ModuleAudience.Everyone);
        var raw = Module("clash2", GrantArea.RawDatabase, ModuleAudience.Everyone);

        var result = AccessCatalog.From(ModuleCatalog.Create([module, raw]).GetOrElse(ModuleCatalog.Empty));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void From_RejectsMenuItemsUsingACoreMenuItemId()
    {
        var module = new ModuleDescriptor("clash", "Clash")
        {
            MenuItems = [new ModuleMenuItem(BuiltInMenus.Main.Items[0].Id, "Clash", "#/clash", 1)]
        };

        Assert.False(AccessCatalog.From(ModuleCatalog.Create([module]).GetOrElse(ModuleCatalog.Empty)).IsValid);
    }

    [Fact]
    public void From_RejectsAdministrationPagesNeedingUnknownGrants()
    {
        var module = new ModuleDescriptor("pages", "Pages")
        {
            AdministrationPages = [new ModuleAdministrationPage("pages.title", "fa-x", "#/manage/pages", "Pages.Read")]
        };

        Assert.False(AccessCatalog.From(ModuleCatalog.Create([module]).GetOrElse(ModuleCatalog.Empty)).IsValid);
    }

    [Fact]
    public void From_AcceptsAdministrationPagesNeedingCoreGrants()
    {
        var module = new ModuleDescriptor("pages", "Pages")
        {
            AdministrationPages = [new ModuleAdministrationPage("pages.title", "fa-x", "#/manage/pages", "Site.Read")]
        };

        Assert.True(AccessCatalog.From(ModuleCatalog.Create([module]).GetOrElse(ModuleCatalog.Empty)).IsValid);
    }

    [Fact]
    public void GrantAction_SharesTheCommonActionNames()
    {
        Assert.Equal(GrantActions.All, GrantAction.All);
        Assert.Equal(GrantActions.Manage, GrantAction.Manage);
    }
}
