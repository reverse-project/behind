using System.Net;
using Behind.Common.WebApi.Infrastructure.StaticFiles;
using Behind.Tests.Helpers;

namespace Behind.Tests.CommonWebApi.StaticFiles;

public class StaticFrontendTests
{
    private const string Index = "<!DOCTYPE html>\n<html>\n  <body>\n    <!-- remove -->\n    <p>home</p>\n  </body>\n</html>\n";

    private static readonly string Script =
        "// remove\nvar answer = 40 + 2;\n" + string.Concat(Enumerable.Repeat("console.log(answer);\n", 200));

    private static TempFrontendDirectory CreateFrontend() =>
        new(new Dictionary<string, string>
        {
            ["index.html"] = Index,
            ["app.js"] = Script,
            ["css/app.css"] = "body {\n    margin: 0;\n}\n"
        });

    private static HttpRequestMessage Get(string path, string? acceptEncoding = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (acceptEncoding is not null)
        {
            request.Headers.AcceptEncoding.ParseAdd(acceptEncoding);
        }

        return request;
    }

    [Fact]
    public async Task Root_ServesIndexHtmlByDefault()
    {
        using var directory = CreateFrontend();
        await using var host = await StaticFrontendHost.StartAsync(directory.Provider);

        var response = await host.Client.SendAsync(Get("/"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<p>home", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Root_CustomDefaultFile_IsServed()
    {
        using var directory = new TempFrontendDirectory(new Dictionary<string, string> { ["home.html"] = "<p>custom</p>" });
        await using var host = await StaticFrontendHost.StartAsync(
            directory.Provider,
            new FrontendOptions { DefaultFile = "home.html" });

        var response = await host.Client.SendAsync(Get("/"));

        Assert.Contains("custom", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Assets_AreMinifiedByDefault()
    {
        using var directory = CreateFrontend();
        await using var host = await StaticFrontendHost.StartAsync(directory.Provider);

        var html = await host.Client.GetStringAsync("/");
        var script = await host.Client.GetStringAsync("/app.js");
        var css = await host.Client.GetStringAsync("/css/app.css");

        Assert.DoesNotContain("remove", html);
        Assert.DoesNotContain("remove", script);
        Assert.Equal("body{margin:0}", css);
    }

    [Fact]
    public async Task Assets_AreNotMinifiedWhenDisabled()
    {
        using var directory = CreateFrontend();
        await using var host = await StaticFrontendHost.StartAsync(
            directory.Provider,
            new FrontendOptions { Minify = false, Compress = false });

        var css = await host.Client.GetStringAsync("/css/app.css");

        Assert.Equal("body {\n    margin: 0;\n}\n", css);
    }

    [Theory]
    [InlineData("br", "br")]
    [InlineData("gzip", "gzip")]
    [InlineData("gzip, br", "br")]
    public async Task Assets_AreCompressedByDefault(string acceptEncoding, string expectedEncoding)
    {
        using var directory = CreateFrontend();
        await using var host = await StaticFrontendHost.StartAsync(directory.Provider);

        var response = await host.Client.SendAsync(Get("/app.js", acceptEncoding));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedEncoding, Assert.Single(response.Content.Headers.ContentEncoding));
    }

    [Fact]
    public async Task Assets_AreNotCompressedWithoutAcceptEncoding()
    {
        using var directory = CreateFrontend();
        await using var host = await StaticFrontendHost.StartAsync(directory.Provider);

        var response = await host.Client.SendAsync(Get("/app.js"));

        Assert.Empty(response.Content.Headers.ContentEncoding);
    }

    [Fact]
    public async Task Assets_AreNotCompressedWhenDisabled()
    {
        using var directory = CreateFrontend();
        await using var host = await StaticFrontendHost.StartAsync(
            directory.Provider,
            new FrontendOptions { Compress = false });

        var response = await host.Client.SendAsync(Get("/app.js", "br, gzip"));

        Assert.Empty(response.Content.Headers.ContentEncoding);
    }

    [Fact]
    public async Task Assets_CarryCacheAndSecurityHeaders()
    {
        using var directory = CreateFrontend();
        await using var host = await StaticFrontendHost.StartAsync(directory.Provider);

        var response = await host.Client.SendAsync(Get("/app.js"));

        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Fact]
    public async Task Assets_MatchingIfNoneMatch_Returns304()
    {
        using var directory = CreateFrontend();
        await using var host = await StaticFrontendHost.StartAsync(directory.Provider);
        var first = await host.Client.SendAsync(Get("/app.js"));

        var request = Get("/app.js");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        var second = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task UnknownPath_Returns404()
    {
        using var directory = CreateFrontend();
        await using var host = await StaticFrontendHost.StartAsync(directory.Provider);

        var response = await host.Client.SendAsync(Get("/missing.js"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
