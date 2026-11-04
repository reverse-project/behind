using System.Text;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class RawDatabaseFrontendTests
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
    [InlineData("controllers/rawdatabase.js")]
    [InlineData("viewmodels/rawdatabase.js")]
    [InlineData("views/rawdatabase.html")]
    [InlineData("models/rawdatabase.js")]
    public void Provider_ContainsFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Fact]
    public void Controller_BindsItsViewAndViewModel()
    {
        var controller = Read("controllers/rawdatabase.js");

        Assert.Contains("text!views/rawdatabase.html", controller);
        Assert.Contains("viewmodels/rawdatabase", controller);
        Assert.Contains("createController(view,", controller);
    }

    [Fact]
    public void Route_IsServedBeforeTheSiteSlugs()
    {
        var app = Read("app.js");

        Assert.Contains("\"#/manage/raw-database\": \"controllers/rawdatabase\"", app);
        Assert.True(
            app.IndexOf("\"#/manage/raw-database\"", StringComparison.Ordinal)
            < app.IndexOf("\"#/:slug\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Administration_ListsTheEditorForTheRawDatabaseGrantOnly()
    {
        var administration = Read("viewmodels/administration.js");

        Assert.Contains("administration.pages.rawDatabase", administration);
        Assert.Contains("#/manage/raw-database", administration);
        Assert.Contains("session.hasGrant(\"RawDatabase.Manage\")", administration);
    }

    [Fact]
    public void ViewModel_RequiresTheRawDatabaseGrant()
    {
        var viewModel = Read("viewmodels/rawdatabase.js");

        Assert.Contains("session.hasGrant(\"RawDatabase.Manage\")", viewModel);
        Assert.Contains("window.confirm(", viewModel);
    }

    [Fact]
    public void Model_TalksToTheRawDatabaseEndpoints()
    {
        var model = Read("models/rawdatabase.js");

        Assert.Contains("/raw-database/tables", model);
        Assert.Contains("\"/update\"", model);
        Assert.Contains("\"/delete\"", model);
        Assert.Contains("rawDatabase.conflict.readOnly", model);
    }

    [Fact]
    public void View_OffersEditingSearchSortingAndPaging()
    {
        var view = Read("views/rawdatabase.html");

        Assert.Contains("submit: save", view);
        Assert.Contains("click: newRow", view);
        Assert.Contains("click: $root.sortBy", view);
        Assert.Contains("click: edit", view);
        Assert.Contains("click: remove", view);
        Assert.Contains("<pager", view);
    }
}
