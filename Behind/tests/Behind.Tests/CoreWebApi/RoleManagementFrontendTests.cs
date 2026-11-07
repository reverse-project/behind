using System.Text;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class RoleManagementFrontendTests
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
    [InlineData("controllers/roles.js")]
    [InlineData("viewmodels/roles.js")]
    [InlineData("views/roles.html")]
    [InlineData("viewmodels/useraccess.js")]
    [InlineData("models/access.js")]
    [InlineData("components/accesseditor.js")]
    [InlineData("components/accesseditor.html")]
    public void Provider_ContainsFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Fact]
    public void Routes_AreRegisteredAndLinkedFromAdministration()
    {
        var app = Read("app.js");
        var administration = Read("viewmodels/administration.js");

        Assert.Contains("\"#/roles\": \"controllers/roles\"", app);
        Assert.Contains("ko.components.register(\"access-editor\", { require: \"components/accesseditor\" })", app);
        Assert.Contains("name: \"administration.pages.roles\", icon: \"fa-user-shield\", href: \"#/roles\", allowed: canManageRoles", administration);
        Assert.Contains("session.hasGrant(\"Role.Manage\")", administration);
    }

    [Fact]
    public void Auth_CallsTheRoleAndUserAccessEndpoints()
    {
        var auth = Read("models/auth.js");

        Assert.Contains("http.get(url)", auth);
        Assert.Contains("var url = \"/roles?page=\"", auth);
        Assert.Contains("http.post(\"/roles/\" + roleId + \"/restore\")", auth);
        Assert.Contains("http.post(\"/roles/\" + roleId + \"/hard/delete\")", auth);
        Assert.Contains("http.post(\"/roles\", { name: name, isDeveloper: isDeveloper === true })", auth);
        Assert.Contains("http.put(\"/roles/\" + roleId + \"/developer\", { isDeveloper: isDeveloper })", auth);
        Assert.Contains("http.put(\"/roles/\" + roleId + \"/access\", access)", auth);
        Assert.Contains("http.post(\"/roles/\" + roleId + \"/delete\")", auth);
        Assert.Contains("http.post(\"/roles/\" + roleId + \"/reset-grants\")", auth);
        Assert.Contains("http.get(\"/users/\" + userId + \"/access\")", auth);
        Assert.Contains("http.put(\"/users/\" + userId + \"/access\", access)", auth);
    }

    [Fact]
    public void RolesView_OffersGrantAndRestrictEditingForEveryRole()
    {
        var view = Read("views/roles.html");

        Assert.All(
            new[]
            {
                "foreach: roles", "submit: create", "click: edit", "click: save", "click: cancel", "click: remove",
                "visible: canEdit", "visible: canDelete", "<access-editor params=\"access: editor", "visible: canManage",
                "visible: isDenied", "foreach: grants", "foreach: restrictions"
            },
            binding => Assert.Contains(binding, view));
        Assert.DoesNotContain("<!--", view);
    }

    [Fact]
    public void AccessEditor_OffersGrantRestrictOrNothingPerGrant()
    {
        var template = Read("components/accesseditor.html");
        var model = Read("models/access.js");

        Assert.Contains("options: $parent.choices", template);
        Assert.Contains("value: state", template);
        Assert.Contains("value: \"grant\"", model);
        Assert.Contains("value: \"restrict\"", model);
        Assert.Contains("value: \"none\"", model);
        Assert.DoesNotContain("<!--", template);
    }

    [Fact]
    public void UsersView_ShowsCalculatedGrantsAndEditsRolesAndDirectAccess()
    {
        var view = Read("views/users.html");
        var viewModel = Read("viewmodels/users.js");
        var access = Read("viewmodels/useraccess.js");

        Assert.Contains("click: access.toggle", view);
        Assert.Contains("foreach: calculatedGrants", view);
        Assert.Contains("checked: $parent.selectedRoleIds", view);
        Assert.Contains("<access-editor params=\"access: editor", view);
        Assert.Contains("click: save", view);
        Assert.DoesNotContain("<!--", view);
        Assert.Contains("new UserAccessViewModel(item.id", viewModel);
        Assert.Contains("auth.saveUserAccess(item.id, body)", viewModel);
        Assert.Contains("access.calculate(chosen, selection.grants, selection.restrictions)", access);
    }

    [Fact]
    public void UsersView_OffersADedicatedRolesPanelNextToTheAccessPanel()
    {
        var view = Read("views/users.html");
        var access = Read("viewmodels/useraccess.js");

        Assert.Contains("click: access.toggleRoles", view);
        Assert.Contains("visible: access.isRolesOpen, with: access", view);
        Assert.Contains("click: save, t: 'users.saveRoles'", view);
        Assert.Contains("self.toggleRoles = function", access);
        Assert.Contains("self.isRolesOpen(false)", access);
        Assert.Contains("self.isOpen(false)", access);
    }

    [Fact]
    public void UserModel_PrefersTheCalculatedGrantsFromTheServer()
    {
        var user = Read("models/user.js");

        Assert.Contains("Array.isArray(user.grants)", user);
    }

    [Fact]
    public void RolesView_SearchesPagesAndHandlesDeletedRoles()
    {
        var view = Read("views/roles.html");
        var viewModel = Read("viewmodels/roles.js");

        Assert.All(
            new[]
            {
                "submit: list.find", "value: list.search", "checked: list.showDeleted", "<letter-pager", "params=\"pager: list.letterPager\"",
                "<pager class=\"d-block\" params=\"pager: list.pager\"", "click: restore", "click: hardDelete", "visible: isDeleted"
            },
            binding => Assert.Contains(binding, view));
        Assert.Contains("createManagedList", viewModel);
        Assert.Contains("auth.restoreRole(row.id)", viewModel);
        Assert.Contains("auth.hardDeleteRole(row.id)", viewModel);
    }

    [Fact]
    public void RolesView_OffersResettingTheGrantsOfBuiltInRoles()
    {
        var view = Read("views/roles.html");
        var viewModel = Read("viewmodels/roles.js");

        Assert.Contains("visible: canResetGrants", view);
        Assert.Contains("click: resetGrants", view);
        Assert.Contains("canResetGrants: item.canResetGrants", viewModel);
        Assert.Contains("auth.resetRoleGrants(row.id)", viewModel);
    }
}
