using System.Text;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class AccessSettingsFrontendTests
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
    [InlineData("controllers/accesssettings.js")]
    [InlineData("viewmodels/accesssettings.js")]
    [InlineData("views/accesssettings.html")]
    [InlineData("models/accesssettings.js")]
    [InlineData("controllers/restriction.js")]
    [InlineData("viewmodels/restriction.js")]
    [InlineData("views/restriction.html")]
    [InlineData("models/restriction.js")]
    public void Provider_ContainsFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Fact]
    public void Controllers_BindTheirViewsAndViewModels()
    {
        var settings = Read("controllers/accesssettings.js");
        var restriction = Read("controllers/restriction.js");

        Assert.Contains("text!views/accesssettings.html", settings);
        Assert.Contains("viewmodels/accesssettings", settings);
        Assert.Contains("text!views/restriction.html", restriction);
        Assert.Contains("viewmodels/restriction", restriction);
    }

    [Fact]
    public void Router_ServesTheAccessSettingsPageBeforeTheAliasCatchAll()
    {
        var app = Read("app.js");

        Assert.Contains("\"#/manage/access-settings\": \"controllers/accesssettings\"", app);
        Assert.True(
            app.IndexOf("\"#/manage/access-settings\"", StringComparison.Ordinal)
            < app.IndexOf("var aliasRoute = \"#/:slug\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Router_ShowsTheRestrictionPageInsteadOfEveryControllerExceptTheSignInPages()
    {
        var app = Read("app.js");

        Assert.Contains("restriction.restricted() && openControllers.indexOf(requested) < 0", app);
        Assert.Contains("var restrictionController = \"controllers/restriction\"", app);
        foreach (var open in new[] { "signin", "signup", "magiclink", "confirmemail", "profilepassword" })
        {
            Assert.Contains($"\"controllers/{open}\"", app);
        }
    }

    [Fact]
    public void Router_ReloadsTheRestrictionOnSessionChangesAndRerunsTheRoute()
    {
        var app = Read("app.js");

        Assert.Contains("session.isSignedIn.subscribe(syncRestriction)", app);
        Assert.Contains("session.isImpersonating.subscribe(syncRestriction)", app);
        Assert.Contains("restriction.restricted.subscribe(", app);
        Assert.Contains("router.runRoute(\"get\", window.location.hash || \"#/\")", app);
    }

    [Fact]
    public void RichEditor_LeavesOutImagesWhenThereIsNoUpload()
    {
        var editor = Read("bindings/richeditor.js");

        Assert.Contains("var withImages = typeof upload === \"function\"", editor);
        Assert.Contains("withImages ? [\"link\", \"image\"] : [\"link\"]", editor);
    }

    [Fact]
    public void RestrictionModel_ReadsTheRestrictionStatus()
    {
        var model = Read("models/restriction.js");

        Assert.Contains("http.get(\"/access/restriction\")", model);
        Assert.Contains("response.data.restricted", model);
        Assert.Contains("response.data.message", model);
        Assert.Contains("response.data.title", model);
    }

    [Fact]
    public void RestrictionView_ShowsTheCustomMessageAndTheSignInLinks()
    {
        var viewModel = Read("viewmodels/restriction.js");
        var view = Read("views/restriction.html");

        Assert.Contains("restriction.message() || i18n.t(\"restriction.defaultMessage\")", viewModel);
        Assert.Contains("restriction.title() || i18n.t(\"restriction.title\")", viewModel);
        Assert.Contains("html: message", view);
        Assert.Contains("text: title", view);
        Assert.Contains("href=\"#/sign-in\"", view);
        Assert.Contains("href=\"#/sign-up\"", view);
        Assert.Contains("behind-restriction-message", view);
    }

    [Fact]
    public void AccessSettingsModel_CallsTheAccessEndpoints()
    {
        var model = Read("models/accesssettings.js");

        Assert.Contains("\"/access/manage\" + (deleted ? \"?deleted=true\" : \"\")", model);
        Assert.Contains("http.post(\"/access\", draft(fields))", model);
        Assert.Contains("http.post(\"/access/delete\")", model);
        Assert.Contains("http.postForm(\"/access/images\", form)", model);
        Assert.Contains("http.post(\"/access/restore\")", model);
        Assert.Contains("http.post(\"/access/hard/delete\")", model);
        Assert.Contains("paging.managing(\"/access/pending-users\", query)", model);
        Assert.Contains("\"/access/pending-users/\" + userId + \"/approve\"", model);
        Assert.Contains("\"/access/pending-users/\" + userId + \"/reject\"", model);
    }

    [Fact]
    public void AccessSettingsViewModel_OffersSaveResetRestoreAndPermanentDeleteByGrant()
    {
        var viewModel = Read("viewmodels/accesssettings.js");
        var view = Read("views/accesssettings.html");

        Assert.Contains("session.hasGrant(\"Access.Read\")", viewModel);
        Assert.Contains("session.hasGrant(\"Access.Write\")", viewModel);
        Assert.Contains("session.hasGrant(\"Access.Delete\")", viewModel);
        Assert.Contains("session.hasGrant(\"RawDatabase.Manage\")", viewModel);
        Assert.Contains("submit: save", view);
        Assert.Contains("click: reset", view);
        Assert.Contains("click: restore", view);
        Assert.Contains("click: hardDelete", view);
        Assert.Contains("showDeleted", view);
    }

    [Fact]
    public void AccessSettingsView_EditsTheRestrictionRoleMessageAndTheSignUpModeration()
    {
        var view = Read("views/accesssettings.html");

        Assert.Contains("checked: restrictionEnabled", view);
        Assert.Contains("options: roles, value: restrictedToRole", view);
        Assert.Contains("richEditor: { value: restrictionMessage, upload: uploadImage", view);
        Assert.Contains("value: restrictionTitle", view);
        Assert.Contains("checked: moderatedSignUp", view);
        Assert.Contains("click: approve", view);
        Assert.Contains("click: reject", view);
    }

    [Fact]
    public void Administration_OffersTheAccessSettingsToAccessReaders()
    {
        var administration = Read("viewmodels/administration.js");

        Assert.Contains("href: \"#/manage/access-settings\", allowed: canReadAccessSettings", administration);
        Assert.Contains("session.hasGrant(\"Access.Read\")", administration);
    }

    [Fact]
    public void AuditLog_NamesTheAccessSettingsAndApprovalEntities()
    {
        var auditLog = Read("viewmodels/auditlog.js");

        Assert.Contains("AccessSettings: \"audit.entities.AccessSettings\"", auditLog);
        Assert.Contains("PendingUserApprovals: \"audit.entities.PendingUserApprovals\"", auditLog);
    }

    [Fact]
    public void Auth_ExplainsThatAnAccountIsWaitingForApproval()
    {
        var auth = Read("models/auth.js");

        Assert.Contains("response.data.code === \"approvalPending\"", auth);
        Assert.Contains("auth.approvalPending", auth);
        Assert.Contains("auth.signUp.pendingApproval", auth);
    }
}
