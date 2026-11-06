using System.Text;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class ImpersonationFrontendTests
{
    private static readonly IFileProvider Provider = EmbeddedFrontend.CreateFileProvider();

    private static string Read(string path)
    {
        var file = Provider.GetFileInfo(path);
        Assert.True(file.Exists, path);

        using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Fact]
    public void Auth_CallsTheImpersonationEndpoints()
    {
        var auth = Read("models/auth.js");

        Assert.Contains("impersonateUser: function (userId)", auth);
        Assert.Contains("http.post(\"/users/\" + userId + \"/impersonate\")", auth);
        Assert.Contains("session.impersonate(response.data)", auth);
        Assert.Contains("stopImpersonating: function ()", auth);
        Assert.Contains("http.post(\"/impersonation/stop\")", auth);
        Assert.Contains("session.stopImpersonating()", auth);
    }

    [Fact]
    public void Session_KeepsTheImpersonatorTokensApartAndRestoresThemOnStop()
    {
        var session = Read("models/session.js");

        Assert.Contains("\"behind.impersonator\"", session);
        Assert.Contains("isImpersonating: ko.pureComputed", session);
        Assert.Contains("impersonate: function (response)", session);
        Assert.Contains("stopImpersonating: function ()", session);
        Assert.Contains("stash(tokens())", session);
        Assert.Contains("persist(original)", session);
    }

    [Fact]
    public void Session_DropsAStaleImpersonationWhenTheServerDoesNotConfirmIt()
    {
        var session = Read("models/session.js");
        var auth = Read("models/auth.js");

        Assert.Contains("confirmImpersonation: function (impersonated)", session);
        Assert.Contains("session.confirmImpersonation(!!response.data.impersonator)", auth);
        Assert.Contains("return verified() && stashed() !== null && tokens() !== null;", session);
        Assert.Contains("session.confirmImpersonation();", auth);
        Assert.Matches(@"start: function \(response\) \{\s*stash\(null\);\s*persist\(begin\(response\)\);", session);
    }

    [Fact]
    public void Header_ShowsABannerWithAStopButtonWhileImpersonating()
    {
        var index = Read("index.html");
        var header = Read("viewmodels/header.js");

        Assert.Contains("css: { 'd-none': !isImpersonating() }", index);
        Assert.Contains("border-bottom d-none\" role=\"alert\"", index);
        Assert.DoesNotContain("style=\"display", index);
        Assert.Contains("click: stopImpersonating", index);
        Assert.Contains("self.isImpersonating = session.isImpersonating", header);
        Assert.Contains("auth.stopImpersonating()", header);
    }

    [Fact]
    public void UsersView_OffersImpersonationWhereThePermissionsAllowIt()
    {
        var view = Read("views/users.html");
        var viewModel = Read("viewmodels/users.js");

        Assert.Contains("visible: canImpersonate, enable: !$root.busy(), click: impersonate", view);
        Assert.Contains("canImpersonate: item.permissions.canImpersonate", viewModel);
        Assert.Contains("auth.impersonateUser(item.id)", viewModel);
    }

    [Fact]
    public void AuditLogViewModel_HighlightsImpersonationActions()
    {
        var viewModel = Read("viewmodels/auditlog.js");

        Assert.Contains("ImpersonationStarted: \"text-bg-warning\"", viewModel);
        Assert.Contains("ImpersonationEnded: \"text-bg-warning\"", viewModel);
    }
}
