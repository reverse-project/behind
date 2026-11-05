using System.Text;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class RouteAliasesFrontendTests
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
    [InlineData("controllers/routealiases.js")]
    [InlineData("controllers/routealias.js")]
    [InlineData("viewmodels/routealiases.js")]
    [InlineData("views/routealiases.html")]
    [InlineData("models/routealiases.js")]
    public void Provider_ContainsFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Fact]
    public void ManagerController_BindsItsViewAndViewModel()
    {
        var controller = Read("controllers/routealiases.js");

        Assert.Contains("text!views/routealiases.html", controller);
        Assert.Contains("viewmodels/routealiases", controller);
        Assert.Contains("createController(view,", controller);
    }

    [Fact]
    public void Routes_ServeTheManagerBeforeTheAliasCatchAll()
    {
        var app = Read("app.js");

        Assert.Contains("\"#/manage/route-aliases\": \"controllers/routealiases\"", app);
        Assert.True(
            app.IndexOf("\"#/manage/route-aliases\"", StringComparison.Ordinal)
            < app.IndexOf("var aliasRoute = \"#/:slug\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Router_ResolvesAnUnknownSlugAsAnAliasBeforeShowingASite()
    {
        var app = Read("app.js");

        Assert.Contains("var aliasController = \"controllers/routealias\"", app);
        Assert.Contains("var siteController = \"controllers/site\"", app);
        Assert.Contains("execute(following ? siteController : aliasController, this.params.toHash())", app);
        Assert.DoesNotContain("\"#/:slug\": \"controllers/site\"", app);
    }

    [Fact]
    public void Router_FollowsAnAliasTargetWithoutChangingTheAddressAndOnlyForKnownRoutes()
    {
        var app = Read("app.js");

        Assert.Contains("router.lookupRoute(\"get\", hash)", app);
        Assert.Contains("router.runRoute(\"get\", hash)", app);
    }

    [Fact]
    public void AliasController_FollowsTheTargetAndFallsBackToTheSite()
    {
        var controller = Read("controllers/routealias.js");

        Assert.Contains("routeAliases.resolve(context.params.slug)", controller);
        Assert.Contains("context.follow(result.data.target)", controller);
        Assert.Contains("context.fallback(context.params)", controller);
    }

    [Fact]
    public void RouteAliasesModel_CallsTheRouteAliasEndpoints()
    {
        var model = Read("models/routealiases.js");

        Assert.Contains("\"/route-aliases/resolve/\" + encodeURIComponent(alias)", model);
        Assert.Contains("managing(\"/route-aliases/manage\", query)", model);
        Assert.Contains("http.post(\"/route-aliases\", draft(fields))", model);
        Assert.Contains("http.post(\"/route-aliases/\" + id, draft(fields))", model);
        Assert.Contains("http.post(\"/route-aliases/\" + id + \"/delete\")", model);
        Assert.Contains("http.post(\"/route-aliases/\" + id + \"/restore\")", model);
        Assert.Contains("http.post(\"/route-aliases/\" + id + \"/hard/delete\")", model);
    }

    [Fact]
    public void ViewModel_OffersSoftDeleteRestoreAndPermanentDeleteByGrant()
    {
        var viewModel = Read("viewmodels/routealiases.js");
        var view = Read("views/routealiases.html");

        Assert.Contains("session.hasGrant(\"RouteAlias.Read\")", viewModel);
        Assert.Contains("session.hasGrant(\"RouteAlias.Write\")", viewModel);
        Assert.Contains("session.hasGrant(\"RouteAlias.Delete\")", viewModel);
        Assert.Contains("click: remove", view);
        Assert.Contains("click: restore", view);
        Assert.Contains("click: hardDelete", view);
        Assert.Contains("showDeleted", view);
    }

    [Fact]
    public void Administration_OffersTheRouteAliasesToRouteAliasReaders()
    {
        var administration = Read("viewmodels/administration.js");

        Assert.Contains("href: \"#/manage/route-aliases\", allowed: canReadRouteAliases", administration);
        Assert.Contains("session.hasGrant(\"RouteAlias.Read\")", administration);
    }

    [Fact]
    public void AuditLog_NamesTheRouteAliasEntity()
    {
        Assert.Contains("RouteAlias: \"audit.entities.RouteAlias\"", Read("viewmodels/auditlog.js"));
    }
}
