using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Behind.MarkdownViewer.Common;
using Behind.MarkdownViewer.WebApi;
using Behind.MarkdownViewer.WebApi.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Behind.Tests.MarkdownViewer;

public sealed class MarkdownViewerEndpointsTests : IAsyncLifetime
{
    private readonly MarkdownSourceTestBed _bed = new MarkdownSourceTestBed()
        .With("README.md", "# Welcome\n\n[setup](guide/setup.md) ![logo](img/logo.png)")
        .With("guide/setup.md", "# Setup")
        .WithBytes("img/logo.png", [1, 2, 3])
        .With("img/vector.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");

    private WebApplication _application = null!;
    private HttpClient _client = null!;
    private MarkdownViewerConfiguration _configuration = null!;

    public async Task InitializeAsync()
    {
        _configuration = _bed.Configuration;

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { ContentRootPath = _bed.Root });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<IOptionsMonitor<MarkdownViewerConfiguration>>(
            new ConfigurationMonitor(() => _configuration));
        builder.Services.AddSingleton<MarkdownWorkspaceProvider>();
        builder.Services.AddAuthorization();

        _application = builder.Build();
        _application.MapMarkdownViewerEndpoints();
        _application.MapPost("/generate-test", MarkdownViewerEndpoints.Generate);
        await _application.StartAsync();

        var address = _application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!
            .Addresses.First();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _application.DisposeAsync();
        _bed.Dispose();
    }

    private sealed class ConfigurationMonitor(Func<MarkdownViewerConfiguration> current)
        : IOptionsMonitor<MarkdownViewerConfiguration>
    {
        public MarkdownViewerConfiguration CurrentValue => current();

        public MarkdownViewerConfiguration Get(string? name) => current();

        public IDisposable? OnChange(Action<MarkdownViewerConfiguration, string?> listener) => null;
    }

    [Fact]
    public async Task List_ReturnsTheTitleAndTheDocuments()
    {
        var response = await _client.GetFromJsonAsync<JsonNode>("/markdownviewer/documents");

        Assert.True((bool)response!["configured"]!);
        Assert.Equal("Docs", (string)response["title"]!);
        Assert.Equal("README.md", (string)response["defaultDocument"]!);
        Assert.Empty(response["locales"]!.AsArray());
        Assert.Null(response["locale"]);
        Assert.Equal(
            [("guide/setup.md", "Setup"), ("README.md", "Welcome")],
            response["entries"]!.AsArray().Select(entry => ((string)entry!["path"]!, (string)entry["title"]!)));
    }

    [Fact]
    public async Task List_GroupsTheDocumentsIntoSectionsBelowTheHome()
    {
        var response = await _client.GetFromJsonAsync<JsonNode>("/markdownviewer/documents");

        Assert.Equal("README.md", (string)response!["home"]!);
        var sections = response["sections"]!.AsArray();
        Assert.Equal("Guide", (string)sections.Single()!["title"]!);
        Assert.Equal(["guide/setup.md"], sections.Single()!["entries"]!.AsArray().Select(entry => (string)entry!["path"]!));
    }

    [Fact]
    public async Task List_WithoutASourceDirectory_ReportsThatItIsNotConfigured()
    {
        _configuration = _configuration with { SourceDirectory = null };

        var response = await _client.GetFromJsonAsync<JsonNode>("/markdownviewer/documents");

        Assert.False((bool)response!["configured"]!);
        Assert.Empty(response["entries"]!.AsArray());
    }

    [Fact]
    public async Task List_FollowsTheConfigurationChanges()
    {
        _configuration = _configuration with { Title = "Other" };

        var response = await _client.GetFromJsonAsync<JsonNode>("/markdownviewer/documents");

        Assert.Equal("Other", (string)response!["title"]!);
    }

    [Fact]
    public async Task Page_ReturnsTheRenderedDocument()
    {
        var response = await _client.GetFromJsonAsync<JsonNode>("/markdownviewer/documents/README.md");

        Assert.Equal("README.md", (string)response!["path"]!);
        Assert.Equal("Welcome", (string)response["title"]!);
        var html = (string)response["html"]!;
        Assert.Contains("href=\"#/docs/guide%2Fsetup.md\"", html);
        Assert.Contains("src=\"/markdownviewer/files/img/logo.png\"", html);
    }

    [Fact]
    public async Task Page_FindsNestedDocuments()
    {
        var response = await _client.GetFromJsonAsync<JsonNode>("/markdownviewer/documents/guide/setup.md");

        Assert.Equal("Setup", (string)response!["title"]!);
    }

    [Theory]
    [InlineData("/markdownviewer/documents/missing.md")]
    [InlineData("/markdownviewer/documents/img/logo.png")]
    [InlineData("/markdownviewer/documents/..%2FREADME.md")]
    [InlineData("/markdownviewer/documents/%2E%2E/README.md")]
    [InlineData("/markdownviewer/files/missing.png")]
    [InlineData("/markdownviewer/files/README.md")]
    [InlineData("/markdownviewer/files/..%2Fdocs%2Fimg%2Flogo.png")]
    public async Task Get_UnknownPaths_AreNotFound(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Page_WithoutASourceDirectory_IsNotFound()
    {
        _configuration = _configuration with { SourceDirectory = null };

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/markdownviewer/documents/README.md")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/markdownviewer/files/img/logo.png")).StatusCode);
    }

    [Fact]
    public async Task Asset_ReturnsTheImageWithoutSniffing()
    {
        var response = await _client.GetAsync("/markdownviewer/files/img/logo.png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(new byte[] { 1, 2, 3 }, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-cache", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Asset_IsolatesVectorImages()
    {
        var response = await _client.GetAsync("/markdownviewer/files/img/vector.svg");

        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("sandbox", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task Generate_WritesTheStaticSite()
    {
        var response = await _client.PostAsync("/generate-test", null);
        var body = await response.Content.ReadFromJsonAsync<JsonNode>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (int)body!["pages"]!);
        Assert.Equal(2, (int)body["assets"]!);
        Assert.Equal(0, (int)body["removed"]!);
        Assert.Equal(_bed.Output, (string)body["outputDirectory"]!);
        Assert.True(_bed.OutputExists("README.html"));
    }

    [Theory]
    [InlineData(null, "output-not-configured")]
    [InlineData("docs", "output-overlaps-source")]
    public async Task Generate_WithAnInvalidOutput_ReportsTheProblem(string? output, string error)
    {
        _configuration = _configuration with { OutputDirectory = output };

        var response = await _client.PostAsync("/generate-test", null);
        var body = await response.Content.ReadFromJsonAsync<JsonNode>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([error], body!["errors"]!.AsArray().Select(item => (string)item!));
    }

    [Fact]
    public async Task Generate_WithoutASourceDirectory_ReportsTheProblem()
    {
        _configuration = _configuration with { SourceDirectory = null };

        var response = await _client.PostAsync("/generate-test", null);
        var body = await response.Content.ReadFromJsonAsync<JsonNode>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["source-not-configured"], body!["errors"]!.AsArray().Select(item => (string)item!));
    }

    [Fact]
    public async Task Generate_WhenTheOutputCannotBeWritten_AnswersWithAServerError()
    {
        File.WriteAllText(_bed.Output, "a file, not a directory");

        var response = await _client.PostAsync("/generate-test", null);
        var body = await response.Content.ReadFromJsonAsync<JsonNode>();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(["generation-failed"], body!["errors"]!.AsArray().Select(item => (string)item!));
    }

    [Fact]
    public void Routes_OnlyTheGenerationNeedsTheWriteGrant()
    {
        var endpoints = _application.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith("/markdownviewer"))
            .ToDictionary(
                endpoint => endpoint.RoutePattern.RawText!,
                endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy).ToList());

        Assert.Equal([MarkdownViewerModuleNames.WriteGrant], endpoints["/markdownviewer/generate"]);
        Assert.Empty(endpoints["/markdownviewer/documents"]);
        Assert.Empty(endpoints["/markdownviewer/documents/{**path}"]);
        Assert.Empty(endpoints["/markdownviewer/files/{**path}"]);
    }
}

public sealed class MarkdownViewerLocaleEndpointsTests : IAsyncLifetime
{
    private readonly MarkdownSourceTestBed _bed = new MarkdownSourceTestBed()
        .With("README.md", "# All languages\n\n[English](en-US/README.md) [Magyar](hu-HU/README.md)")
        .With("en-US/README.md", "# Welcome\n\n![logo](logo.png) ![shared](only-english.png) [all](../README.md)")
        .With("en-US/guide.md", "# Guide")
        .With("hu-HU/README.md", "# Üdv\n\n![logo](logo.png)")
        .WithBytes("en-US/logo.png", [1, 2, 3])
        .WithBytes("hu-HU/logo.png", [4, 5, 6])
        .WithBytes("en-US/only-english.png", [7, 8, 9]);

    private WebApplication _application = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var configuration = _bed.Configuration;
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { ContentRootPath = _bed.Root });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["Frontend:Site:DefaultLocale"] = "en-US";
        builder.Services.AddSingleton<IOptionsMonitor<MarkdownViewerConfiguration>>(new FixedMonitor(configuration));
        builder.Services.AddSingleton<MarkdownWorkspaceProvider>();

        _application = builder.Build();
        _application.MapMarkdownViewerEndpoints();
        await _application.StartAsync();

        var address = _application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!
            .Addresses.First();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _application.DisposeAsync();
        _bed.Dispose();
    }

    private sealed class FixedMonitor(MarkdownViewerConfiguration value) : IOptionsMonitor<MarkdownViewerConfiguration>
    {
        public MarkdownViewerConfiguration CurrentValue => value;

        public MarkdownViewerConfiguration Get(string? name) => value;

        public IDisposable? OnChange(Action<MarkdownViewerConfiguration, string?> listener) => null;
    }

    [Theory]
    [InlineData("hu-HU", "hu-HU", new[] { "Üdv" })]
    [InlineData("en-US", "en-US", new[] { "Guide", "Welcome" })]
    [InlineData("fr-FR", "en-US", new[] { "Guide", "Welcome" })]
    [InlineData(null, "en-US", new[] { "Guide", "Welcome" })]
    public async Task List_ReturnsTheDocumentsOfTheChosenLocale(string? locale, string expected, string[] titles)
    {
        var query = locale is null ? string.Empty : $"?locale={locale}";

        var response = await _client.GetFromJsonAsync<JsonNode>($"/markdownviewer/documents{query}");

        Assert.Equal(["en-US", "hu-HU"], response!["locales"]!.AsArray().Select(item => (string)item!));
        Assert.Equal(expected, (string)response["locale"]!);
        Assert.Equal(titles, response["entries"]!.AsArray().Select(entry => (string)entry!["title"]!));
    }

    [Fact]
    public async Task Page_ReturnsTheDocumentInTheChosenLocale()
    {
        var response = await _client.GetFromJsonAsync<JsonNode>("/markdownviewer/documents/README.md?locale=hu-HU");

        Assert.Equal("hu-HU", (string)response!["locale"]!);
        Assert.Equal("Üdv", (string)response["title"]!);
        Assert.Contains("src=\"/markdownviewer/files/logo.png?locale=hu-HU\"", (string)response["html"]!);
    }

    [Fact]
    public async Task Page_FallsBackToTheDefaultLocaleWhenTheDocumentIsMissing()
    {
        var response = await _client.GetFromJsonAsync<JsonNode>("/markdownviewer/documents/guide.md?locale=hu-HU");

        Assert.Equal("en-US", (string)response!["locale"]!);
        Assert.Equal("Guide", (string)response["title"]!);
    }

    [Fact]
    public async Task Page_WithAnUnknownLocale_UsesTheDefaultLocale()
    {
        var response = await _client.GetFromJsonAsync<JsonNode>("/markdownviewer/documents/README.md?locale=..%2Fx");

        Assert.Equal("en-US", (string)response!["locale"]!);
    }

    [Fact]
    public async Task Page_WithoutTheDocumentInAnyLocale_IsNotFound()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/markdownviewer/documents/missing.md?locale=hu-HU")).StatusCode);
    }

    [Fact]
    public async Task Asset_ComesFromTheChosenLocaleAndFallsBackToTheDefaultOne()
    {
        var hungarian = await _client.GetByteArrayAsync("/markdownviewer/files/logo.png?locale=hu-HU");
        var fallback = await _client.GetByteArrayAsync("/markdownviewer/files/only-english.png?locale=hu-HU");

        Assert.Equal(new byte[] { 4, 5, 6 }, hungarian);
        Assert.Equal(new byte[] { 7, 8, 9 }, fallback);
    }

    [Fact]
    public async Task Asset_WithPathsOutsideTheLocaleFolder_IsNotFound()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/markdownviewer/files/..%2Fhu-HU%2Flogo.png?locale=en-US")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/markdownviewer/files/hu-HU/logo.png?locale=en-US")).StatusCode);
    }

    [Fact]
    public async Task Page_ServesTheRootDocumentsAsSharedPages()
    {
        var response = await _client.GetFromJsonAsync<JsonNode>("/markdownviewer/documents/~/README.md?locale=hu-HU");

        Assert.Equal("~/README.md", (string)response!["path"]!);
        Assert.Equal("All languages", (string)response["title"]!);
        Assert.Null(response["locale"]);
        var html = (string)response["html"]!;
        Assert.Contains("href=\"#/docs/README.md?locale=en-US\"", html);
        Assert.Contains("href=\"#/docs/README.md?locale=hu-HU\"", html);
    }

    [Fact]
    public async Task Page_LinksTheLocaleDocumentsToTheSharedPage()
    {
        var response = await _client.GetFromJsonAsync<JsonNode>("/markdownviewer/documents/README.md?locale=en-US");

        Assert.Contains("href=\"#/docs/%7E%2FREADME.md\"", (string)response!["html"]!);
    }

    [Theory]
    [InlineData("/markdownviewer/documents/~/missing.md")]
    [InlineData("/markdownviewer/documents/~/en-US/README.md")]
    [InlineData("/markdownviewer/documents/~/")]
    public async Task Page_UnknownSharedDocuments_AreNotFound(string url)
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(url)).StatusCode);
    }
}
