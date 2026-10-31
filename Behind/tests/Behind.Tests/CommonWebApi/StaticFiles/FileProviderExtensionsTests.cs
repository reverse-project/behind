using System.Net;
using Behind.Common.WebApi.Infrastructure.StaticFiles;
using Behind.Tests.Helpers;

namespace Behind.Tests.CommonWebApi.StaticFiles;

public class FileProviderExtensionsTests
{
    private static TempFrontendDirectory Embedded() =>
        new(new Dictionary<string, string>
        {
            ["index.html"] = "<p>embedded</p>",
            ["app.js"] = "var embedded = 1;"
        });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WithWebRoot_NoPath_ReturnsSameProvider(string? webRoot)
    {
        using var embedded = Embedded();

        Assert.Same(embedded.Provider, embedded.Provider.WithWebRoot(webRoot));
    }

    [Fact]
    public void WithWebRoot_MissingDirectory_ReturnsSameProvider()
    {
        using var embedded = Embedded();
        var missing = Path.Combine(embedded.Root, "wwwroot");

        Assert.Same(embedded.Provider, embedded.Provider.WithWebRoot(missing));
    }

    [Fact]
    public void WithWebRoot_ExistingDirectory_PrefersWebRootFiles()
    {
        using var embedded = Embedded();
        using var webRoot = new TempFrontendDirectory(new Dictionary<string, string> { ["index.html"] = "<p>wwwroot</p>" });

        var provider = embedded.Provider.WithWebRoot(webRoot.Root);

        using var reader = new StreamReader(provider.GetFileInfo("index.html").CreateReadStream());
        Assert.Equal("<p>wwwroot</p>", reader.ReadToEnd());
    }

    [Fact]
    public void WithWebRoot_ExistingDirectory_FallsBackToInnerProvider()
    {
        using var embedded = Embedded();
        using var webRoot = new TempFrontendDirectory(new Dictionary<string, string> { ["index.html"] = "<p>wwwroot</p>" });

        var provider = embedded.Provider.WithWebRoot(webRoot.Root);

        Assert.True(provider.GetFileInfo("app.js").Exists);
    }

    [Fact]
    public async Task UseStaticFrontend_ServesWebRootImagesAlongsideEmbeddedFiles()
    {
        byte[] image = [0x89, 0x50, 0x4E, 0x47, 0x00, 0xFF];
        using var embedded = Embedded();
        using var webRoot = new TempFrontendDirectory(new Dictionary<string, string>());
        webRoot.WriteBytes("images/logo.png", image);
        await using var host = await StaticFrontendHost.StartAsync(embedded.Provider.WithWebRoot(webRoot.Root));

        var logo = await host.Client.GetAsync("/images/logo.png");
        var home = await host.Client.GetStringAsync("/");

        Assert.Equal("image/png", logo.Content.Headers.ContentType?.MediaType);
        Assert.Equal(image, await logo.Content.ReadAsByteArrayAsync());
        Assert.Contains("embedded", home);
    }

    [Fact]
    public async Task UseStaticFrontend_WebRootOverridesEmbeddedFile()
    {
        using var embedded = Embedded();
        using var webRoot = new TempFrontendDirectory(new Dictionary<string, string> { ["app.js"] = "var overridden = 2;" });
        await using var host = await StaticFrontendHost.StartAsync(
            embedded.Provider.WithWebRoot(webRoot.Root),
            new FrontendOptions { Minify = false, Compress = false });

        var response = await host.Client.GetAsync("/app.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("overridden", await response.Content.ReadAsStringAsync());
    }
}
