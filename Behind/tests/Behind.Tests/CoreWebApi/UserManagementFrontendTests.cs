using System.Text;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class UserManagementFrontendTests
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
    [InlineData("controllers/users.js")]
    [InlineData("viewmodels/users.js")]
    [InlineData("views/users.html")]
    [InlineData("controllers/wallofshame.js")]
    [InlineData("viewmodels/wallofshame.js")]
    [InlineData("views/wallofshame.html")]
    public void Provider_ContainsFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Fact]
    public void Routes_AreRegisteredAndLinked()
    {
        var app = Read("app.js");
        var index = Read("index.html");
        var administration = Read("viewmodels/administration.js");

        Assert.Contains("\"#/users\": \"controllers/users\"", app);
        Assert.Contains("\"#/wall-of-shame\": \"controllers/wallofshame\"", app);
        Assert.Contains("name: \"Users\", icon: \"fa-users\", href: \"#/users\", allowed: canManageUsers", administration);
        Assert.Contains("<a class=\"nav-link\" href=\"#/wall-of-shame\">Wall of shame</a>", index);
        Assert.Contains("href=\"#/administration\">Administration</a>", index);
    }

    [Fact]
    public void Header_ShowsTheAdministrationLinkOnlyToUserManagers()
    {
        var header = Read("viewmodels/header.js");
        var session = Read("models/session.js");
        var auth = Read("models/auth.js");

        Assert.Contains("session.hasGrant(\"User.Manage\")", header);
        Assert.Contains("visible: canAdminister", Read("index.html"));
        Assert.Contains("session.grants() === null", header);
        Assert.Contains("hasGrant: function", session);
        Assert.Contains("session.identify(user.label(response.data.user), user.grants(response.data.user))", auth);
    }

    [Fact]
    public void Auth_CallsTheUserManagementEndpoints()
    {
        var auth = Read("models/auth.js");

        Assert.Contains("\"/users?page=\"", auth);
        Assert.Contains("encodeURIComponent(query.search)", auth);
        Assert.Contains("\"/elevate/\"", auth);
        Assert.Contains("\"/deelevate/\"", auth);
        Assert.Contains("\"/sign/out\"", auth);
        Assert.Contains("\"/ban\", { reason: reason }", auth);
        Assert.Contains("\"/unban\"", auth);
        Assert.Contains("\"/delete\"", auth);
        Assert.Contains("\"/restore\"", auth);
        Assert.Contains("\"/hard/delete\"", auth);
        Assert.Contains("\"&deleted=true\"", auth);
        Assert.Contains("\"/wallOfShame\"", auth);
    }

    [Fact]
    public void SignIn_ExplainsABanWithItsReason()
    {
        var auth = Read("models/auth.js");

        Assert.Contains("403: bannedMessage(response)", auth);
        Assert.Contains("\"This account is banned: \" + reason", auth);
    }

    [Fact]
    public void UsersView_OffersEveryManagementAction()
    {
        var view = Read("views/users.html");

        Assert.All(
            new[]
            {
                "foreach: users", "foreach: roleActions", "click: run", "click: signOut", "click: startBan",
                "click: unban", "submit: confirmBan", "click: cancelBan", "value: banMessage", "submit: find",
                "visible: canManage", "visible: isDenied", "params=\"pager: pager\""
            },
            binding => Assert.Contains(binding, view));
        Assert.DoesNotContain("<!--", view);
    }

    [Fact]
    public void UsersViewModel_OnlyOffersActionsTheServerAllows()
    {
        var viewModel = Read("viewmodels/users.js");

        Assert.Contains("item.permissions[definition.allowed]", viewModel);
        Assert.Contains("item.permissions.canModerate", viewModel);
        Assert.Contains("auth.elevateUser(", viewModel);
        Assert.Contains("auth.deelevateUser(", viewModel);
        Assert.Contains("auth.signOutUser(", viewModel);
        Assert.Contains("auth.banUser(item.id, row.banMessage().trim())", viewModel);
        Assert.Contains("auth.unbanUser(", viewModel);
        Assert.Contains("window.confirm(", viewModel);
        Assert.Contains("models/pager", viewModel);
    }

    [Fact]
    public void LetterPager_IsARegisteredReusableComponentUsedByTheUsersPage()
    {
        var app = Read("app.js");
        var component = Read("components/letterpager.js");
        var template = Read("components/letterpager.html");
        var model = Read("models/letterpager.js");
        var view = Read("views/users.html");
        var viewModel = Read("viewmodels/users.js");
        var auth = Read("models/auth.js");

        Assert.Contains("ko.components.register(\"letter-pager\", { require: \"components/letterpager\" })", app);
        Assert.Contains("text!components/letterpager.html", component);
        Assert.Contains("params.pager", component);
        Assert.All(
            new[] { "click: all", "foreach: letters", "$parent.select", "text: label", "enable: isAvailable" },
            binding => Assert.Contains(binding, template));
        Assert.DoesNotContain("user", template, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("user", model, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("params=\"pager: letterPager\"", view);
        Assert.Contains("models/letterpager", viewModel);
        Assert.Contains("letter: self.letterPager.selected()", viewModel);
        Assert.Contains("self.letterPager.update(result.data.letters)", viewModel);
        Assert.Contains("encodeURIComponent(query.letter)", auth);
    }

    [Fact]
    public void LetterPager_ShowsEveryLetterAndTheOtherBucketAndResetsThePage()
    {
        var model = Read("models/letterpager.js");
        var viewModel = Read("viewmodels/users.js");

        Assert.Contains("\"ABCDEFGHIJKLMNOPQRSTUVWXYZ#\"", model);
        Assert.Contains("present.indexOf(letter) >= 0 || letter === selected", model);
        Assert.Contains("self.pager.page(1)", viewModel);
    }

    [Fact]
    public void UsersPage_IsWiderAndKeepsTheLetterBarOnOneLine()
    {
        var css = Read("css/app.css");
        var view = Read("views/users.html");
        var template = Read("components/letterpager.html");

        Assert.Contains(".behind-main:has(.behind-wide)", css);
        Assert.Contains("<section class=\"behind-wide\">", view);
        Assert.Contains("flex-nowrap", template);
        Assert.Contains("overflow-x-auto", template);
        Assert.DoesNotContain("flex-wrap", template);
    }

    [Fact]
    public void WallOfShame_IsPublicAndListsBanMessages()
    {
        var view = Read("views/wallofshame.html");
        var viewModel = Read("viewmodels/wallofshame.js");

        Assert.Contains("foreach: entries", view);
        Assert.Contains("text: reason", view);
        Assert.Contains("params=\"pager: pager\"", view);
        Assert.Contains("auth.loadWallOfShame(", viewModel);
        Assert.DoesNotContain("session.isSignedIn", viewModel);
    }

    [Fact]
    public void UsersPage_OffersDeleteRestoreAndPermanentDeleteOnlyWhenAllowed()
    {
        var view = Read("views/users.html");
        var viewModel = Read("viewmodels/users.js");

        Assert.All(
            new[]
            {
                "visible: canDelete", "click: remove", "visible: canManageDeleted", "click: restore",
                "click: hardDelete", "checked: showDeleted", "visible: canViewDeleted", "visible: isDeleted"
            },
            binding => Assert.Contains(binding, view));
        Assert.Contains("item.permissions.canDelete", viewModel);
        Assert.Contains("item.permissions.canManageDeleted", viewModel);
        Assert.Contains("session.hasGrant(\"RawDatabase.Manage\")", viewModel);
        Assert.Contains("deleted: self.showDeleted()", viewModel);
        Assert.Contains("auth.deleteUser(", viewModel);
        Assert.Contains("auth.restoreUser(", viewModel);
        Assert.Contains("auth.hardDeleteUser(", viewModel);
    }
}
