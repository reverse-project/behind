using System.Text;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class AccountsFrontendTests
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
    public void Model_ReadsTheSettings()
    {
        var model = Read("models/accounts.js");

        Assert.Contains("http.get(\"/accounts/settings\")", model);
        Assert.Contains("var password = ko.observable(true)", model);
        Assert.Contains("var magicLink = ko.observable(true)", model);
        Assert.Contains("var twoFactor = ko.observable(true)", model);
        Assert.Contains("var passkey = ko.observable(true)", model);
        Assert.Contains("signIn(response.data.signIn)", model);
        Assert.Contains("signUp(response.data.signUp)", model);
        Assert.Contains("password(response.data.password !== false)", model);
        Assert.Contains("magicLink(response.data.magicLink !== false)", model);
        Assert.Contains("twoFactor(response.data.twoFactor !== false)", model);
        Assert.Contains("passkey(response.data.passkey !== false)", model);
        Assert.Contains("accounts.load()", Read("app.js"));
    }

    [Fact]
    public void SignIn_HidesDisabledMethodsAndKeepsLostPasswordAction()
    {
        var view = Read("views/signin.html");
        var viewModel = Read("viewmodels/signin.js");

        Assert.Contains("if: passwordSignInEnabled", view);
        Assert.Contains("visible: passwordSignInEnabled", view);
        Assert.Contains("visible: magicLinkSignInEnabled", view);
        Assert.Contains("click: requestLostPassword", view);
        Assert.Contains("self.passwordSignInEnabled = accounts.password", viewModel);
        Assert.Contains("self.magicLinkSignInEnabled = accounts.magicLink", viewModel);
        Assert.Contains("if (!accounts.password() || self.busy())", viewModel);
    }

    [Fact]
    public void Profile_AlwaysOffersFactorManagement()
    {
        var view = Read("views/profile.html");

        Assert.Contains("href=\"#/profile/two-factor\"", view);
        Assert.Contains("profile.twoFactorManage", view);
    }

    [Fact]
    public void Header_ShowsTheButtonsOnlyWhileTheyAreEnabled()
    {
        var template = Read("components/siteheader.html");
        var header = Read("viewmodels/header.js");

        Assert.Contains("visible: !isSignedIn() && canSignIn()\"><a class=\"nav-link\" href=\"#/sign-in\"", template);
        Assert.Contains("visible: !isSignedIn() && canSignUp()\"><a class=\"nav-link\" href=\"#/sign-up\"", template);
        Assert.Contains("self.canSignIn = accounts.signIn", header);
        Assert.Contains("self.canSignUp = accounts.signUp", header);
    }
}
