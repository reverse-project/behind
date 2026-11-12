using System.Text.Json;
using Behind.Common.Configuration;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter;
using Instrumentation = Behind.Common.Configuration.OpenTelemetryConfiguration.Instrumentation;

namespace Behind.Tests.Sites;

public class RpDotHuSiteTests
{
    private static DirectoryInfo SolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory;
    }

    private static string SiteFile(string name) =>
        Path.Combine(SolutionDirectory().FullName, "src", "Sites", "Behind.Sites.RPDotHu", name);

    private static string Read(string name)
    {
        var path = SiteFile(name);
        Assert.True(File.Exists(path), name);

        return File.ReadAllText(path);
    }

    private static JsonElement ReadJson(string name)
    {
        using var document = JsonDocument.Parse(Read(name));
        return document.RootElement.Clone();
    }

    [Fact]
    public void OpenTelemetry_BindsAllConfiguredInstruments()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(SiteFile("appsettings.json"));
        var telemetry = configuration.GetSection(OpenTelemetryConfiguration.SectionName)
            .Get<OpenTelemetryConfiguration>(options => options.ErrorOnUnknownConfiguration = true);

        Assert.NotNull(telemetry);
        Assert.True(telemetry.IsEnabled);
        Assert.Equal("rpdothu-localhost-dev", telemetry.ServiceName);
        Assert.NotNull(telemetry.Traces);
        Assert.NotNull(telemetry.Metrics);
        Assert.NotNull(telemetry.Logging);
        Assert.True(telemetry.Traces.IsEnabled);
        Assert.True(telemetry.Metrics.IsEnabled);
        Assert.True(telemetry.Logging.IsEnabled);
        Assert.Equal(OtlpExportProtocol.HttpProtobuf, telemetry.Traces.Protocol);
        Assert.Equal(OtlpExportProtocol.HttpProtobuf, telemetry.Metrics.Protocol);
        Assert.Equal(OtlpExportProtocol.HttpProtobuf, telemetry.Logging.Protocol);
        Assert.Equal(
            [Instrumentation.AspNetCoreInstrumentation, Instrumentation.HttpClientInstrumentation, Instrumentation.OtlpExporter],
            telemetry.Traces.Instruments);
        Assert.Equal(
            [Instrumentation.AspNetCoreInstrumentation, Instrumentation.HttpClientInstrumentation, Instrumentation.RuntimeInstrumentation, Instrumentation.OtlpExporter],
            telemetry.Metrics.Instruments);
        Assert.Equal([Instrumentation.OtlpExporter], telemetry.Logging.Instruments);
    }

    [Theory]
    [InlineData("wwwroot/index.html")]
    [InlineData("wwwroot/css/rpdothu.css")]
    [InlineData("wwwroot/logo.png")]
    [InlineData("wwwroot/favicon.svg")]
    [InlineData("wwwroot/favicon.ico")]
    [InlineData("wwwroot/favicon-96x96.png")]
    [InlineData("wwwroot/apple-touch-icon.png")]
    [InlineData("wwwroot/web-app-manifest-192x192.png")]
    [InlineData("wwwroot/web-app-manifest-512x512.png")]
    [InlineData("wwwroot/site.webmanifest")]
    public void Wwwroot_ContainsOverrideFile(string path)
    {
        Assert.True(File.Exists(SiteFile(path)), path);
    }

    [Fact]
    public void Logo_IsAPngImage()
    {
        var header = File.ReadAllBytes(SiteFile("wwwroot/logo.png")).Take(8).ToArray();

        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, header);
    }

    [Fact]
    public void IndexHtml_LoadsSkinAfterAppCss()
    {
        var index = Read("wwwroot/index.html");
        var appCss = index.IndexOf("css/app.css", StringComparison.Ordinal);
        var skin = index.IndexOf("css/rpdothu.css", StringComparison.Ordinal);

        Assert.True(appCss >= 0, "css/app.css");
        Assert.True(skin > appCss, "css/rpdothu.css after css/app.css");
    }

    [Theory]
    [InlineData("<header id=\"header-container\">")]
    [InlineData("<main id=\"main-container\"")]
    [InlineData("<footer id=\"footer-container\"")]
    [InlineData("libraries/require.min.js")]
    [InlineData("data-main=\"bootloader.js\"")]
    [InlineData("spinner-border")]
    [InlineData("<menu-items params=\"items: links, variant: 'navbar', hover: true\">")]
    [InlineData("<menu-nav params=\"name: 'footer'\">")]
    public void IndexHtml_KeepsTheEmbeddedShell(string marker)
    {
        Assert.Contains(marker, Read("wwwroot/index.html"));
    }

    [Theory]
    [InlineData("rp-menu")]
    [InlineData("rp-brand-logo")]
    [InlineData("src=\"/logo.png\"")]
    [InlineData("rp-contact")]
    [InlineData("rp-bottom")]
    [InlineData("Ábel Zoltán E.V.")]
    [InlineData("Debrecen")]
    [InlineData("59357311-1-29")]
    [InlineData("57377928")]
    [InlineData("mailto:abel.zoltan@reverseproject.hu")]
    [InlineData("tel:+36303661148")]
    public void IndexHtml_ShowsTheBrandAndTheSoleProprietor(string marker)
    {
        Assert.Contains(marker, Read("wwwroot/index.html"));
    }

    [Fact]
    public void IndexHtml_UsesReverseProjectBranding()
    {
        var index = Read("wwwroot/index.html");

        Assert.Contains("<title>Reverse Project</title>", index);
        Assert.Contains("content=\"Reverse Project\"", index);
        Assert.Contains("<meta name=\"color-scheme\" content=\"light\">", index);
    }

    [Theory]
    [InlineData("--bs-primary: #14347f")]
    [InlineData("--bs-primary-rgb: 20, 52, 127")]
    [InlineData("--bs-secondary: #ffffff")]
    [InlineData(":is(#header-container, .behind-page-header) .rp-menu")]
    [InlineData("linear-gradient(180deg, #ffffff")]
    [InlineData("--bs-navbar-color: var(--rp-blue)")]
    [InlineData(":is(#footer-container, .behind-page-footer) .rp-contact")]
    [InlineData(":is(#footer-container, .behind-page-footer) .rp-bottom")]
    [InlineData(".behind-carousel")]
    [InlineData(".behind-section-accented")]
    [InlineData(".ql-snow .ql-tooltip .ql-link-target input")]
    public void Skin_UsesTheReverseProjectDesign(string marker)
    {
        Assert.Contains(marker, Read("wwwroot/css/rpdothu.css"));
    }

    [Theory]
    [InlineData("color-scheme: light only")]
    [InlineData("--bs-body-color: #1f2937")]
    [InlineData(".text-body-secondary")]
    [InlineData(".text-muted")]
    [InlineData(".btn-secondary")]
    [InlineData(".form-select option")]
    [InlineData("--bs-table-bg: #ffffff")]
    [InlineData(".form-switch .form-check-input:not(:checked)")]
    public void Skin_KeepsTextReadable(string marker)
    {
        Assert.Contains(marker, Read("wwwroot/css/rpdothu.css"));
    }

    [Fact]
    public void WebManifest_UsesReverseProjectBranding()
    {
        var manifest = ReadJson("wwwroot/site.webmanifest");

        Assert.Equal("Reverse Project", manifest.GetProperty("name").GetString());
        Assert.Equal("RP", manifest.GetProperty("short_name").GetString());
        Assert.Equal("#14347f", manifest.GetProperty("theme_color").GetString());
    }

    [Fact]
    public void Project_IsRegisteredInTheSolution()
    {
        var solution = File.ReadAllText(Path.Combine(SolutionDirectory().FullName, "Behind.slnx"));

        Assert.Contains("src/Sites/Behind.Sites.RPDotHu/Behind.Sites.RPDotHu.csproj", solution);
    }

    [Fact]
    public void FrontendJson_UsesReverseProjectBranding()
    {
        var site = ReadJson("frontend.json").GetProperty("Frontend").GetProperty("Site");

        Assert.Equal("Reverse Project", site.GetProperty("Title").GetString());
        Assert.Equal("hu-HU", site.GetProperty("DefaultLocale").GetString());
        Assert.Equal("abel.zoltan@reverseproject.hu", site.GetProperty("ContactEmail").GetString());
    }

    [Fact]
    public void Css_SecondaryColorIsVisibleOnTheLightBackground()
    {
        var css = Read("wwwroot/css/rpdothu.css");

        Assert.Matches(@"\.text-bg-secondary,\s*\.bg-secondary \{[^}]*background-color: var\(--rp-silver\) !important;", css);
        Assert.Matches(@"\.link-secondary \{[^}]*color: var\(--rp-text-muted\) !important;", css);
    }

    [Fact]
    public void Css_NavbarTogglerIconIsSetOnTheIconItself()
    {
        var css = Read("wwwroot/css/rpdothu.css");

        Assert.Contains(":is(#header-container, .behind-page-header) .rp-menu .navbar-toggler-icon {", css);
        Assert.Matches(@"\.navbar-toggler-icon \{\s*--bs-navbar-toggler-icon-bg: url\(", css);
    }

    [Fact]
    public void Index_HidesTheImpersonationBannerUntilTheBindingsAreApplied()
    {
        var index = Read("wwwroot/index.html");

        Assert.Contains("border-bottom d-none\" role=\"alert\" data-bind=\"css: { 'd-none': !isImpersonating() }\"", index);
    }

    [Fact]
    public void DatabaseJson_UsesItsOwnDatabase()
    {
        var connection = ReadJson("database.json")
            .GetProperty("ConnectionStrings")
            .GetProperty("DefaultConnection")
            .GetString();

        Assert.Contains("rphu.db", connection);
    }

    [Fact]
    public void BackendJson_UsesItsOwnDataDirectory()
    {
        var dataDirectory = ReadJson("backend.json")
            .GetProperty("Backend")
            .GetProperty("DataDirectory")
            .GetString();

        Assert.Contains("rphu", dataDirectory);
    }

    [Fact]
    public void SecurityJson_DoesNotShareSecretsWithOtherSites()
    {
        var fse = Path.Combine(SolutionDirectory().FullName, "src", "Sites", "Behind.Sites.FSE", "security.json");
        var signingKey = ReadJson("security.json").GetProperty("Security").GetProperty("Jwt").GetProperty("SigningKey").GetString();

        Assert.DoesNotContain(signingKey!, File.ReadAllText(fse));
    }

    [Fact]
    public void ContainerPublishScript_ExistsForTheSite()
    {
        var script = Path.Combine(SolutionDirectory().FullName, "tools", "publish-sites-rpdothu-container.sh");

        Assert.Contains("Behind.Sites.RPDotHu", File.ReadAllText(script));
    }
}
