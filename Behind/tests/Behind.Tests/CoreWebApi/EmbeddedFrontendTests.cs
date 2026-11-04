using System.Text;
using Behind.Common.WebApi.Infrastructure.StaticFiles;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class EmbeddedFrontendTests
{
    private static readonly IFileProvider Provider = EmbeddedFrontend.CreateFileProvider();

    private static IEnumerable<IFileInfo> Walk(string path) =>
        Provider.GetDirectoryContents(path).SelectMany(entry =>
            entry.IsDirectory ? Walk($"{path.TrimEnd('/')}/{entry.Name}") : [entry]);

    private static string Read(IFileInfo file)
    {
        using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Theory]
    [InlineData("index.html")]
    [InlineData("bootloader.js")]
    [InlineData("config.js")]
    [InlineData("app.js")]
    [InlineData("libraries/require.min.js")]
    [InlineData("libraries/jquery.min.js")]
    [InlineData("libraries/knockout.min.js")]
    [InlineData("libraries/sammy.min.js")]
    [InlineData("libraries/text.js")]
    [InlineData("libraries/domReady.js")]
    [InlineData("views/index.html")]
    [InlineData("controllers/index.js")]
    [InlineData("viewmodels/index.js")]
    [InlineData("models/session.js")]
    [InlineData("controllers/magiclink.js")]
    [InlineData("viewmodels/magiclink.js")]
    [InlineData("views/magiclink.html")]
    [InlineData("controllers/confirmemail.js")]
    [InlineData("viewmodels/confirmemail.js")]
    [InlineData("views/confirmemail.html")]
    [InlineData("controllers/session.js")]
    [InlineData("viewmodels/session.js")]
    [InlineData("views/session.html")]
    [InlineData("viewmodels/header.js")]
    [InlineData("controllers/administration.js")]
    [InlineData("viewmodels/administration.js")]
    [InlineData("views/administration.html")]
    [InlineData("css/fontawesome.min.css")]
    [InlineData("css/solid.min.css")]
    [InlineData("webfonts/fa-solid-900.woff2")]
    [InlineData("controllers/sessions.js")]
    [InlineData("viewmodels/sessions.js")]
    [InlineData("views/sessions.html")]
    [InlineData("controllers/profile.js")]
    [InlineData("viewmodels/profile.js")]
    [InlineData("views/profile.html")]
    [InlineData("models/user.js")]
    [InlineData("models/pager.js")]
    [InlineData("components/pager.js")]
    [InlineData("components/pager.html")]
    public void Provider_ContainsFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Fact]
    public void IndexHtml_LoadsRequireWithBootloader()
    {
        var index = Read(Provider.GetFileInfo("index.html"));

        Assert.Contains("libraries/require.min.js", index);
        Assert.Contains("data-main=\"bootloader.js\"", index);
    }

    [Fact]
    public void IndexHtml_ShowsLoadingSpinnerInsideTheMainContainer()
    {
        var index = Read(Provider.GetFileInfo("index.html"));
        var main = index[index.IndexOf("<main id=\"main-container\"", StringComparison.Ordinal)..index.IndexOf("</main>", StringComparison.Ordinal)];

        Assert.Contains("spinner-border", main);
        Assert.Contains("role=\"status\"", main);
        Assert.Contains("visually-hidden", main);
    }

    [Fact]
    public void ConfigPaths_PointToExistingLibraries()
    {
        var config = Read(Provider.GetFileInfo("config.js"));

        var paths = System.Text.RegularExpressions.Regex
            .Matches(config, "\"?[\\w-]+\"?: \"(libraries/[^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .ToList();

        Assert.NotEmpty(paths);
        Assert.All(paths, path => Assert.True(Provider.GetFileInfo($"{path}.js").Exists, path));
    }

    [Fact]
    public void ApplicationSources_AreMinifiable()
    {
        var sources = Walk("/")
            .Where(file => !file.Name.Contains(".min."))
            .ToList();

        var failures = ContentMinifiers.Default
            .SelectMany(minifier => sources
                .Where(file => minifier.Extensions.Contains(Path.GetExtension(file.Name)))
                .Where(file => minifier.Minify(Read(file)).Equals(None))
                .Select(file => file.Name))
            .ToList();

        Assert.NotEmpty(sources);
        Assert.Empty(failures);
    }

    [Fact]
    public void Views_AreMinifiedThroughProvider()
    {
        var provider = new MinifyingFileProvider(Provider, ContentMinifiers.Default);

        var original = Read(Provider.GetFileInfo("views/signin.html"));
        var minified = Read(provider.GetFileInfo("views/signin.html"));

        Assert.True(minified.Length < original.Length);
        Assert.Contains("data-bind", minified);
    }

    [Fact]
    public void MagicLinkRedemption_PostsTokenInBodyAndScrubsItFromTheUrl()
    {
        var auth = Read(Provider.GetFileInfo("models/auth.js"));
        var viewModel = Read(Provider.GetFileInfo("viewmodels/magiclink.js"));

        Assert.Contains("http.post(\"/sign/in/magiclink/redeem\"", auth);
        Assert.DoesNotContain("magiclink?token=", auth);
        Assert.Contains("context.scrub(", viewModel);
    }

    [Fact]
    public void Scrub_RecordsTheScrubbedHashAsTheRouterLocationSoTheRouteDoesNotRunTwice()
    {
        var app = Read(Provider.GetFileInfo("app.js"));

        Assert.Contains("replaceState(", app);
        Assert.Contains("router.last_location = [\"get\", router.getLocation()]", app);
    }

    [Fact]
    public void SessionRoute_IsRegistered()
    {
        var app = Read(Provider.GetFileInfo("app.js"));

        Assert.Contains("\"#/session\": \"controllers/session\"", app);
        Assert.Contains("viewmodels/header", app);
    }

    [Fact]
    public void Header_ShowsUserNameMenuWithTheUserMenuItemsAndSignOutWhenSignedIn_AndSignInLinksOtherwise()
    {
        var index = Read(Provider.GetFileInfo("index.html"));
        var header = Read(Provider.GetFileInfo("viewmodels/header.js"));
        var menu = index[index.IndexOf("nav-item dropdown", StringComparison.Ordinal)..];
        menu = menu[..menu.IndexOf("</div>", StringComparison.Ordinal)];

        Assert.Contains("<li class=\"nav-item dropdown\" data-bind=\"visible: isSignedIn\">", index);
        Assert.Contains("data-bs-toggle=\"dropdown\" aria-expanded=\"false\" data-bind=\"text: label\"", index);
        Assert.Contains("params=\"items: userItems, variant: 'dropdown'\"", menu);
        Assert.Contains("click: signOut", menu);
        Assert.Contains("params=\"items: links, variant: 'navbar'\"", index);
        Assert.Contains("<li class=\"nav-item\" data-bind=\"visible: !isSignedIn() && canSignIn()\"><a class=\"nav-link\" href=\"#/sign-in\" data-bind=\"t: 'app.signIn'\">", index);
        Assert.Contains("<li class=\"nav-item\" data-bind=\"visible: !isSignedIn() && canSignUp()\"><a class=\"nav-link\" href=\"#/sign-up\" data-bind=\"t: 'app.signUp'\">", index);
        Assert.Equal(1, index.Split("click: signOut").Length - 1);
        Assert.Contains("menus.follow(\"main\")", header);
        Assert.Contains("session.displayName()", header);
        Assert.Contains("auth.loadSession()", header);
        Assert.Contains("identify(session.isSignedIn())", header);
    }

    [Fact]
    public void AdministrationRoute_IsRegisteredAndRendersAGridOfIconButtons()
    {
        var app = Read(Provider.GetFileInfo("app.js"));
        var index = Read(Provider.GetFileInfo("index.html"));
        var view = Read(Provider.GetFileInfo("views/administration.html"));

        Assert.Contains("\"#/administration\": \"controllers/administration\"", app);
        Assert.Contains("css/fontawesome.min.css", index);
        Assert.Contains("css/solid.min.css", index);
        Assert.Contains("foreach: pages", view);
        Assert.Contains("row row-cols-", view);
        Assert.Contains("css: icon", view);
        Assert.Contains("text: name", view);
        Assert.Contains("attr: { href: href }", view);
    }

    [Fact]
    public void AdministrationViewModel_ListsTheUsersRolesSitesNewsMenusAndAuditLogPagesForTheirManagers()
    {
        var viewModel = Read(Provider.GetFileInfo("viewmodels/administration.js"));
        var view = Read(Provider.GetFileInfo("views/administration.html"));

        Assert.Equal(12, viewModel.Split("icon: \"fa-").Length - 1);
        Assert.Contains("name: \"administration.pages.menus\", icon: \"fa-bars\", href: \"#/manage/menus\", allowed: canReadMenus", viewModel);
        Assert.Contains("hasGrant(\"Menu.Read\")", viewModel);
        Assert.Contains("name: \"administration.pages.users\", icon: \"fa-users\", href: \"#/users\", allowed: canManageUsers", viewModel);
        Assert.Contains("name: \"administration.pages.sites\", icon: \"fa-file-lines\", href: \"#/sites\", allowed: canReadSites", viewModel);
        Assert.Contains("name: \"administration.pages.news\", icon: \"fa-newspaper\", href: \"#/manage/news\", allowed: canReadNews", viewModel);
        Assert.Contains("hasGrant(\"User.Manage\")", viewModel);
        Assert.Contains("hasGrant(\"Site.Read\")", viewModel);
        Assert.Contains("hasGrant(\"News.Read\")", viewModel);
        Assert.Contains("visible: isEmpty", view);
    }

    [Fact]
    public void FontAwesomeAssets_AreSelfHostedAndReferenceExistingFonts()
    {
        var solid = Read(Provider.GetFileInfo("css/solid.min.css"));

        Assert.Contains("Font Awesome Free", solid);
        Assert.Contains("../webfonts/fa-solid-900.woff2", solid);
        Assert.True(Provider.GetFileInfo("webfonts/fa-solid-900.woff2").Exists);
    }

    [Theory]
    [InlineData("css/bootstrap.min.css")]
    [InlineData("libraries/bootstrap.bundle.min.js")]
    public void Provider_ContainsBootstrapAssets(string path)
    {
        var content = Read(Provider.GetFileInfo(path));

        Assert.Matches(@"Bootstrap\s+v5\.3", content);
        Assert.DoesNotContain("sourceMappingURL", content);
    }

    [Fact]
    public void IndexHtml_LoadsBootstrapStylesBeforeApplicationStyles()
    {
        var index = Read(Provider.GetFileInfo("index.html"));

        Assert.True(index.IndexOf("css/bootstrap.min.css", StringComparison.Ordinal) < index.IndexOf("css/app.css", StringComparison.Ordinal));
        Assert.Contains("data-bs-toggle=\"collapse\"", index);
    }

    [Fact]
    public void Bootloader_LoadsBootstrapScripts()
    {
        var config = Read(Provider.GetFileInfo("config.js"));
        var bootloader = Read(Provider.GetFileInfo("bootloader.js"));

        Assert.Contains("bootstrap: \"libraries/bootstrap.bundle.min\"", config);
        Assert.Contains("\"bootstrap\"", bootloader);
    }

    [Fact]
    public void Views_UseBootstrapComponents()
    {
        var views = new[] { "signin", "signup" }.Select(name => Read(Provider.GetFileInfo($"views/{name}.html")));

        Assert.All(views, view =>
        {
            Assert.Contains("form-control", view);
            Assert.Contains("btn btn-primary", view);
        });
    }

    [Fact]
    public void SessionPageLink_IsOnlyInTheBuiltInMainMenu()
    {
        var links = Walk("/")
            .Where(file => file.Name.EndsWith(".html"))
            .Where(file => Read(file).Contains("href=\"#/session\""))
            .Select(file => file.Name)
            .ToList();

        Assert.Empty(links);
        Assert.True(BuiltInMenus.Main.HasItem("Session", "#/session", MenuItemKind.User));
    }

    [Fact]
    public void SessionLoading_SharesTheRenewalAndStoresTheRotatedTokens()
    {
        var auth = Read(Provider.GetFileInfo("models/auth.js"));
        var http = Read(Provider.GetFileInfo("models/http.js"));
        var session = Read(Provider.GetFileInfo("models/session.js"));

        Assert.Contains("http.loadSession()", auth);
        Assert.DoesNotContain("session.refreshHeaders()", auth);
        Assert.Contains("session.identify(user.label(response.data.user), user.grants(response.data.user))", auth);
        Assert.Contains("session.refreshHeaders()", http);
        Assert.Contains("session.renew(response.data.refreshed)", http);
        Assert.Contains("session.end()", http);
        Assert.Contains("extraHeaders", http);
        Assert.Contains("renew:", session);
        Assert.Contains("refreshToken: refreshed.refreshToken", session);
    }

    [Fact]
    public void SessionView_BindsUserRolesAndSessionDetails()
    {
        var view = Read(Provider.GetFileInfo("views/session.html"));

        Assert.All(
            new[] { "displayName", "emails", "roles", "grants", "startedAt", "expiresAt", "accessTokenExpiresAt", "remoteAddress", "userAgent", "click: refresh" },
            binding => Assert.Contains(binding, view));
        Assert.DoesNotContain("<!--", view);
    }

    [Fact]
    public void Http_RenewsSessionOnceAndRetriesUnauthorizedRequests()
    {
        var http = Read(Provider.GetFileInfo("models/http.js"));
        var session = Read(Provider.GetFileInfo("models/session.js"));

        Assert.Contains("response.status !== 401", http);
        Assert.Contains("pendingSession", http);
        Assert.Contains("session.refreshHeaders()", http);
        Assert.Contains("url === sessionUrl", http);
        Assert.Contains("session.renew(response.data.refreshed)", http);
        Assert.Contains("\"X-Refresh-Token\"", session);
    }

    [Fact]
    public void SignUpView_OffersAnOptionalDisplayNameAndSendsIt()
    {
        var view = Read(Provider.GetFileInfo("views/signup.html"));
        var viewModel = Read(Provider.GetFileInfo("viewmodels/signup.js"));
        var auth = Read(Provider.GetFileInfo("models/auth.js"));

        Assert.Contains("value: displayName", view);
        Assert.DoesNotContain("required data-bind=\"value: displayName\"", view);
        Assert.Contains("self.displayName()", viewModel);
        Assert.Contains("displayName: displayName", auth);
    }

    [Fact]
    public void UserLabel_FallsBackToThePrimaryEmailWhenTheDisplayNameIsBlank()
    {
        var user = Read(Provider.GetFileInfo("models/user.js"));
        var auth = Read(Provider.GetFileInfo("models/auth.js"));
        var viewModel = Read(Provider.GetFileInfo("viewmodels/session.js"));

        Assert.Contains("(user.displayName || \"\").trim()", user);
        Assert.Contains("emails[0]", user);
        Assert.Contains("user.label(data.user)", viewModel);
        Assert.Contains("user.label(response.data.user)", auth);
    }

    [Fact]
    public void ProfileRoute_IsRegisteredLinkedAndSavesTheDisplayName()
    {
        var app = Read(Provider.GetFileInfo("app.js"));
        var view = Read(Provider.GetFileInfo("views/profile.html"));
        var auth = Read(Provider.GetFileInfo("models/auth.js"));
        var http = Read(Provider.GetFileInfo("models/http.js"));

        Assert.Contains("\"#/profile\": \"controllers/profile\"", app);
        Assert.True(BuiltInMenus.Main.HasItem("Profile", "#/profile", MenuItemKind.User));
        Assert.Contains("value: displayName", view);
        Assert.Contains("submit: submit", view);
        Assert.Contains("http.put(\"/profile/displayName\"", auth);
        Assert.Contains("put: function", http);
    }

    [Fact]
    public void SessionsRoute_IsRegisteredAndLinkedUnderTheDisplayNameMenu()
    {
        var app = Read(Provider.GetFileInfo("app.js"));

        Assert.Contains("\"#/sessions\": \"controllers/sessions\"", app);
        Assert.True(BuiltInMenus.Main.HasItem("Sessions", "#/sessions", MenuItemKind.User));
    }

    [Fact]
    public void SessionsView_ShowsCurrentSessionsATogglableOldSessionsPartAndSignOutAll()
    {
        var view = Read(Provider.GetFileInfo("views/sessions.html"));

        Assert.All(
            new[]
            {
                "foreach: active", "foreach: ended", "remoteAddress", "userAgent", "isCurrent", "reason",
                "visible: showEnded", "click: toggleEnded", "click: signOutAll", "click: refresh",
                "params=\"pager: activePager\"", "params=\"pager: endedPager\""
            },
            binding => Assert.Contains(binding, view));
        Assert.DoesNotContain("<!--", view);
    }

    [Fact]
    public void SessionsViewModel_LoadsSessionsAndSignsOutEverywhere()
    {
        var auth = Read(Provider.GetFileInfo("models/auth.js"));
        var viewModel = Read(Provider.GetFileInfo("viewmodels/sessions.js"));

        Assert.Contains("http.get(\"/sessions\"", auth);
        Assert.Contains("http.post(\"/sign/out/all\")", auth);
        Assert.Contains("session.end()", auth);
        Assert.Contains("auth.loadSessions(", viewModel);
        Assert.Contains("activePage=", auth);
        Assert.Contains("endedPage=", auth);
        Assert.Contains("pageSize=", auth);
        Assert.Contains("auth.signOutAll()", viewModel);
        Assert.Contains("window.confirm(", viewModel);
        Assert.Contains("self.showEnded = ko.observable(false)", viewModel);
    }

    [Fact]
    public void SessionsView_SeparatesTheCurrentSessionsPagerFromTheOldSessionsToggle()
    {
        var view = Read(Provider.GetFileInfo("views/sessions.html"));

        Assert.Contains("<pager class=\"d-block mb-4\" params=\"pager: activePager\"></pager>", view);
    }

    [Fact]
    public void Pager_IsARegisteredReusableComponentNotTiedToSessions()
    {
        var app = Read(Provider.GetFileInfo("app.js"));
        var component = Read(Provider.GetFileInfo("components/pager.js"));
        var template = Read(Provider.GetFileInfo("components/pager.html"));
        var model = Read(Provider.GetFileInfo("models/pager.js"));
        var sessionsView = Read(Provider.GetFileInfo("views/sessions.html"));
        var sessionsViewModel = Read(Provider.GetFileInfo("viewmodels/sessions.js"));

        Assert.Contains("ko.components.register(\"pager\", { require: \"components/pager\" })", app);
        Assert.Contains("text!components/pager.html", component);
        Assert.Contains("params.pager", component);
        Assert.All(
            new[] { "click: first", "click: previous", "click: next", "click: last", "foreach: pages", "$parent.goTo", "text: label", "visible: hasItems" },
            binding => Assert.Contains(binding, template));
        Assert.DoesNotContain("session", template, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("session", model, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("models/pager", sessionsViewModel);
        Assert.DoesNotContain("pager-template", sessionsView);
        Assert.DoesNotContain("createPager = function", sessionsViewModel);
    }
}
