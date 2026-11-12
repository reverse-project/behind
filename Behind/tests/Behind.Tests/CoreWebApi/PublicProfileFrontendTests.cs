using System.Text;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public sealed class PublicProfileFrontendTests
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
    [InlineData("controllers/publicprofile.js")]
    [InlineData("viewmodels/publicprofile.js")]
    [InlineData("views/publicprofile.html")]
    [InlineData("models/profiles.js")]
    public void PublicProfile_IsEmbedded(string path) =>
        Assert.True(Provider.GetFileInfo(path).Exists);

    [Fact]
    public void PublicProfile_UsesServerPermissionsAndExistingManagementActions()
    {
        Assert.Contains("\"#/user/:id\": \"controllers/publicprofile\"", Read("app.js"));
        var model = Read("viewmodels/publicprofile.js");
        var view = Read("views/publicprofile.html");
        Assert.Contains("profiles.get(id)", model);
        Assert.Contains("auth.banUser(id,", model);
        Assert.Contains("auth.unbanUser(id)", model);
        Assert.Contains("auth.impersonateUser(id)", model);
        Assert.Contains("permissions.canModerate", view);
        Assert.Contains("permissions.canImpersonate", view);
        Assert.Contains("window.confirm", model);
        Assert.DoesNotContain("html:", view);
        Assert.DoesNotContain("birthday", view);
    }

    [Fact]
    public void ProfileSettings_KeepBirthdayPrivateAndEmailHiddenByDefault()
    {
        var model = Read("viewmodels/profile.js");
        var view = Read("views/profile.html");
        Assert.Contains("self.hideEmail = ko.observable(true)", model);
        Assert.Contains("profiles.save(self.birthday(), self.hideEmail())", model);
        Assert.Contains("type=\"date\"", view);
        Assert.Contains("checked: hideEmail", view);
        Assert.DoesNotContain("placeholder: emails()", view);
        Assert.DoesNotContain("user.emails", Read("models/user.js"));
        Assert.Contains("user.href(self.userId())", model);
    }

    [Theory]
    [InlineData("news")]
    [InlineData("articles")]
    [InlineData("galleries")]
    [InlineData("articlereader")]
    [InlineData("galleryviewer")]
    [InlineData("newsmanager")]
    [InlineData("articlemanager")]
    [InlineData("gallerymanager")]
    public void ContentAuthors_LinkToThePublicProfile(string feature)
    {
        Assert.Contains("authorHref", Read($"viewmodels/{feature}.js"));
        Assert.Contains("href: authorHref", Read($"views/{feature}.html"));
    }
}
