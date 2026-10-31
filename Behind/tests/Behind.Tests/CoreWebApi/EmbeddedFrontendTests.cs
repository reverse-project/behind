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
    [InlineData("controllers/session.js")]
    [InlineData("viewmodels/session.js")]
    [InlineData("views/session.html")]
    [InlineData("viewmodels/header.js")]
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
    public void SessionRoute_IsRegistered()
    {
        var app = Read(Provider.GetFileInfo("app.js"));

        Assert.Contains("\"#/session\": \"controllers/session\"", app);
        Assert.Contains("viewmodels/header", app);
    }

    [Fact]
    public void Header_ShowsUserNameAndSignOutWhenSignedIn_AndSignInLinksOtherwise()
    {
        var index = Read(Provider.GetFileInfo("index.html"));
        var header = Read(Provider.GetFileInfo("viewmodels/header.js"));

        Assert.Contains("<a class=\"nav-link\" href=\"#/session\" data-bind=\"text: label\">", index);
        Assert.Contains("<li class=\"nav-item\" data-bind=\"visible: isSignedIn\">", index);
        Assert.Contains("<li class=\"nav-item\" data-bind=\"visible: !isSignedIn()\"><a class=\"nav-link\" href=\"#/sign-in\">", index);
        Assert.Contains("<li class=\"nav-item\" data-bind=\"visible: !isSignedIn()\"><a class=\"nav-link\" href=\"#/sign-up\">", index);
        Assert.Contains("visible: isSignedIn, click: signOut", index);
        Assert.Contains("session.displayName()", header);
        Assert.Contains("auth.loadSession()", header);
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
    public void SessionPageLink_IsOnlyInTheHeader()
    {
        var links = Walk("/")
            .Where(file => file.Name.EndsWith(".html"))
            .Where(file => Read(file).Contains("href=\"#/session\""))
            .Select(file => file.Name)
            .ToList();

        Assert.Equal(["index.html"], links);
    }

    [Fact]
    public void SessionLoading_SendsRefreshTokenAndStoresRefreshedAccessToken()
    {
        var auth = Read(Provider.GetFileInfo("models/auth.js"));
        var http = Read(Provider.GetFileInfo("models/http.js"));
        var session = Read(Provider.GetFileInfo("models/session.js"));

        Assert.Contains("http.get(\"/session\"", auth);
        Assert.Contains("session.refreshHeaders()", auth);
        Assert.Contains("session.renew(response.data.refreshed)", auth);
        Assert.Contains("session.end()", auth);
        Assert.Contains("session.identify(response.data.user.displayName)", auth);
        Assert.Contains("extraHeaders", http);
        Assert.Contains("renew:", session);
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
        Assert.Contains("pendingRenewal", http);
        Assert.Contains("session.refreshHeaders()", http);
        Assert.Contains("url === sessionUrl", http);
        Assert.Contains("session.renew(response.data.refreshed)", http);
        Assert.Contains("\"X-Refresh-Token\"", session);
    }
}
