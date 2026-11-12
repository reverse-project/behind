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
        Assert.Contains("signIn(response.data.signIn)", model);
        Assert.Contains("signUp(response.data.signUp)", model);
        Assert.Contains("accounts.load()", Read("app.js"));
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
