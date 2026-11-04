using System.Text;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class MenusFrontendTests
{
    private static readonly IFileProvider Provider = EmbeddedFrontend.CreateFileProvider();

    private static string Read(string path)
    {
        var file = Provider.GetFileInfo(path);
        Assert.True(file.Exists, path);

        using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Theory]
    [InlineData("controllers/menus.js")]
    [InlineData("controllers/menuitems.js")]
    [InlineData("viewmodels/menus.js")]
    [InlineData("viewmodels/menuitems.js")]
    [InlineData("views/menus.html")]
    [InlineData("views/menuitems.html")]
    [InlineData("models/menus.js")]
    [InlineData("models/paging.js")]
    [InlineData("components/menuitems.js")]
    [InlineData("components/menuitems.html")]
    [InlineData("components/menunav.js")]
    [InlineData("components/menunav.html")]
    public void Provider_ContainsFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Theory]
    [InlineData("menus")]
    [InlineData("menuitems")]
    public void Controllers_BindTheirViewAndViewModel(string name)
    {
        var controller = Read($"controllers/{name}.js");

        Assert.Contains($"text!views/{name}.html", controller);
        Assert.Contains($"viewmodels/{name}", controller);
        Assert.Contains("createController(view,", controller);
    }

    [Fact]
    public void Routes_ServeTheMenuPagesBeforeTheSiteSlugs()
    {
        var app = Read("app.js");

        Assert.Contains("\"#/manage/menus\": \"controllers/menus\"", app);
        Assert.Contains("\"#/manage/menus/:id\": \"controllers/menuitems\"", app);
        Assert.True(
            app.IndexOf("\"#/manage/menus\"", StringComparison.Ordinal)
            < app.IndexOf("\"#/:slug\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Components_AreRegisteredBeforeTheHeaderIsBound()
    {
        var app = Read("app.js");

        Assert.Contains("ko.components.register(\"menu-items\", { require: \"components/menuitems\" })", app);
        Assert.Contains("ko.components.register(\"menu-nav\", { require: \"components/menunav\" })", app);
        Assert.True(
            app.IndexOf("ko.components.register(\"menu-items\"", StringComparison.Ordinal)
            < app.IndexOf("ko.applyBindings(new HeaderViewModel", StringComparison.Ordinal));
    }

    [Fact]
    public void Administration_OffersTheMenusToMenuReaders()
    {
        var administration = Read("viewmodels/administration.js");

        Assert.Contains("href: \"#/manage/menus\", allowed: canReadMenus", administration);
        Assert.Contains("session.hasGrant(\"Menu.Read\")", administration);
    }

    [Fact]
    public void Header_BuildsItsNavigationFromTheMainMenuAndTheFooterOffersAMenuToo()
    {
        var header = Read("viewmodels/header.js");
        var index = Read("index.html");

        Assert.Contains("menus.follow(\"main\")", header);
        Assert.Contains("itemsOfKind(\"Link\")", header);
        Assert.Contains("itemsOfKind(\"User\")", header);
        Assert.Contains("params=\"items: links, variant: 'navbar'\"", index);
        Assert.Contains("params=\"items: userItems, variant: 'dropdown'\"", index);
        Assert.Contains("<menu-nav params=\"name: 'footer'\"></menu-nav>", index);
    }

    [Fact]
    public void MenusModel_CallsTheMenuEndpoints()
    {
        var model = Read("models/menus.js");

        Assert.Contains("\"/menus?\" + names(list)", model);
        Assert.Contains("\"name=\" + encodeURIComponent(name)", model);
        Assert.Contains("managing(\"/menus/manage\", query)", model);
        Assert.Contains("managing(\"/menus/\" + id + \"/items/manage\", query)", model);
        Assert.Contains("http.post(\"/menus\", menuDraft(fields))", model);
        Assert.Contains("http.post(\"/menus/\" + id, menuDraft(fields))", model);
        Assert.Contains("http.post(\"/menus/\" + id + \"/delete\")", model);
        Assert.Contains("http.post(\"/menus/\" + id + \"/restore\")", model);
        Assert.Contains("http.post(\"/menus/\" + id + \"/hard/delete\")", model);
        Assert.Contains("http.post(\"/menus/\" + menuId + \"/items\", itemDraft(fields))", model);
        Assert.Contains("http.post(\"/menus/items/\" + id, itemDraft(fields))", model);
        Assert.Contains("http.post(\"/menus/items/\" + id + \"/delete\")", model);
        Assert.Contains("http.post(\"/menus/items/\" + id + \"/restore\")", model);
        Assert.Contains("http.post(\"/menus/items/\" + id + \"/hard/delete\")", model);
    }

    [Fact]
    public void MenusModel_OffersTheCurrentPageNewTabAndNewWindowTargetsAndOpensWindowsWithoutOpenerAccess()
    {
        var model = Read("models/menus.js");

        Assert.Contains("{ value: \"CurrentPage\", label: \"menuitems.target.currentPage\" }", model);
        Assert.Contains("{ value: \"NewTab\", label: \"menuitems.target.newTab\" }", model);
        Assert.Contains("{ value: \"NewWindow\", label: \"menuitems.target.newWindow\" }", model);
        Assert.Contains("target: opensInNewTab ? \"_blank\" : null", model);
        Assert.Contains("rel: opensInNewTab ? \"noopener noreferrer\" : null", model);
        Assert.Contains("noopener,noreferrer", model);
        Assert.Contains("window.open(item.url, \"_blank\", windowFeatures)", model);
    }

    [Fact]
    public void MenusModel_ReloadsTheFollowedMenuWhenTheSessionChangesAndIgnoresStaleAnswers()
    {
        var model = Read("models/menus.js");

        Assert.Contains("session.isSignedIn.subscribe(reload)", model);
        Assert.Contains("session.grants.subscribe(reload)", model);
        Assert.Contains("ticket === latest", model);
    }

    [Fact]
    public void ArticlesAndNewsModels_ShareThePagingHelpers()
    {
        Assert.Contains("models/paging", Read("models/articles.js"));
        Assert.Contains("models/paging", Read("models/news.js"));
        Assert.Contains("\"&search=\" + encodeURIComponent(query.search)", Read("models/paging.js"));
    }

    [Fact]
    public void MenusViewModel_UsesTheMenuGrantsAndTheManagedList()
    {
        var viewModel = Read("viewmodels/menus.js");

        Assert.Contains("session.hasGrant(\"Menu.Read\")", viewModel);
        Assert.Contains("session.hasGrant(\"Menu.Write\")", viewModel);
        Assert.Contains("session.hasGrant(\"Menu.Delete\")", viewModel);
        Assert.Contains("createManagedList", viewModel);
        Assert.Contains("menus.restore(row.id)", viewModel);
        Assert.Contains("menus.hardDelete(row.id)", viewModel);
        Assert.Contains("itemsHref: \"#/manage/menus/\" + item.id", viewModel);
    }

    [Fact]
    public void MenusView_ListsMenusWithDeletedRecordsForDevelopersAndProtectsTheBuiltInMenu()
    {
        var view = Read("views/menus.html");

        Assert.Contains("Show deleted menus", view);
        Assert.Contains("visible: !isDeleted && !isBuiltIn && $root.canDelete()", view);
        Assert.Contains("click: restore", view);
        Assert.Contains("click: hardDelete", view);
        Assert.Contains("enable: !editingBuiltIn()", view);
        Assert.Contains("attr: { href: itemsHref }", view);
    }

    [Fact]
    public void MenuItemsViewModel_ManagesItemsWithParentsRolesAndSoftDelete()
    {
        var viewModel = Read("viewmodels/menuitems.js");

        Assert.Contains("session.hasGrant(\"Menu.Write\")", viewModel);
        Assert.Contains("session.hasGrant(\"Menu.Delete\")", viewModel);
        Assert.Contains("menus.manageItems(menuId, query)", viewModel);
        Assert.Contains("createManagedList", viewModel);
        Assert.Contains("menus.createItem(menuId, fields)", viewModel);
        Assert.Contains("menus.saveItem(self.editingId(), fields)", viewModel);
        Assert.Contains("menus.removeItem(row.id)", viewModel);
        Assert.Contains("menus.restoreItem(row.id)", viewModel);
        Assert.Contains("menus.hardDeleteItem(row.id)", viewModel);
        Assert.Contains("parentId: self.isUserItem() ? null : self.parentId()", viewModel);
        Assert.Contains("ids.length === 0 ? i18n.t(\"menuitems.allRoles\")", viewModel);
    }

    [Fact]
    public void MenuItemsView_OffersTargetKindParentPositionAndRoles()
    {
        var view = Read("views/menuitems.html");

        Assert.Contains("options: targets", view);
        Assert.Contains("visible: allowsUserItems", view);
        Assert.Contains("options: parentChoices", view);
        Assert.Contains("optionsCaption: noParentLabel", view);
        Assert.Contains("checked: $root.roleIds, checkedValue: id", view);
        Assert.Contains("everyone, including visitors who are not signed in", view);
        Assert.Contains("Show deleted menu items", view);
        Assert.Contains("<letter-pager", view);
        Assert.Contains("<pager", view);
        Assert.Contains("Search menu items", view);
    }

    [Fact]
    public void MenuItemsComponent_RendersLinksDropdownsAndUserMenuEntriesWithTheirTargets()
    {
        var template = Read("components/menuitems.html");
        var script = Read("components/menuitems.js");

        Assert.Contains("attr: attributes", template);
        Assert.Contains("click: open", template);
        Assert.Contains("dropdown-toggle", template);
        Assert.Contains("$component.isDropdown", template);
        Assert.Contains("dropdown: { list: \"list-unstyled mb-0\", item: \"\", link: \"dropdown-item\" }", script);
        Assert.Contains("navbar: { list: \"navbar-nav mb-2 mb-md-0\"", script);
    }

    [Theory]
    [InlineData("components/menuitems.html")]
    [InlineData("components/menunav.html")]
    public void MenuComponentTemplates_AvoidContainerlessBindingsBecauseTheMinifierDropsOptionalEndTags(string path)
    {
        Assert.DoesNotContain("<!-- ko", Read(path));
    }

    [Fact]
    public void MenuNavComponent_ShowsTheNamedMenuThroughTheMenuItemsComponent()
    {
        Assert.Contains("<menu-items params=\"items: items, variant: 'nav'\"></menu-items>", Read("components/menunav.html"));
        Assert.Contains("menus.follow(params.name)", Read("components/menunav.js"));
    }
}
