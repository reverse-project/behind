using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi;

public sealed class SiteSectionsHttpTests : IAsyncDisposable
{
    private readonly NewsTestBed _bed = new();
    private readonly WebApplication _app;
    private readonly HttpClient _client;

    public SiteSectionsHttpTests()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddScoped<ISiteCommands>(_ => NewCommands());
        builder.Services.AddScoped<ISiteQueries>(_ => new SiteQueries(_bed.Read(), _bed.ReadVersions()));

        _app = builder.Build();
        _app.MapPost("/sites", SiteEndpoints.Create);
        _app.MapGet("/sites/slug/{slug}", SiteEndpoints.GetPublic);
        _app.MapGet("/sites/{id:guid}/versions/{version:long}", SiteEndpoints.GetVersion);
        _app.StartAsync().GetAwaiter().GetResult();

        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    private SiteCommands NewCommands()
    {
        var context = _bed.Write();
        var versions = _bed.WriteVersions();
        var files = new VfsCommands(context, _bed.ContentStore);

        return new SiteCommands(
            context,
            versions,
            new FixedTimeProvider(NewsTestBed.Now),
            files,
            new CarouselCommands(context, versions, files));
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
        _bed.Dispose();
    }

    private static string Body(Guid? imageId, string theme = "Accented") =>
        JsonSerializer.Serialize(new
        {
            title = "Landing",
            slug = "landing",
            content = "<p>intro</p>",
            publish = true,
            hideTitle = true,
            sections = new object[]
            {
                new
                {
                    theme,
                    title = "About us",
                    content = "<p>Hello</p><script>x</script>",
                    imageId,
                    imageAlt = "Team",
                    imagePosition = "Right"
                },
                new
                {
                    theme = "Normal",
                    title = "",
                    content = "<p>More</p>",
                    imageId = (Guid?)null,
                    imageAlt = "",
                    imagePosition = "Left"
                }
            }
        });

    private Task<HttpResponseMessage> PostAsync(string json) =>
        _client.PostAsync("/sites", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

    [Fact]
    public async Task Create_ThenGetPublic_RoundTripsTheSectionsAsJson()
    {
        var image = await _bed.AddImageAsync();

        var created = await PostAsync(Body(image));
        var response = await _client.GetAsync("/sites/slug/landing");

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(page.GetProperty("hideTitle").GetBoolean());
        var sections = page.GetProperty("sections").EnumerateArray().ToList();
        Assert.Equal(2, sections.Count);
        Assert.Equal("Accented", sections[0].GetProperty("theme").GetString());
        Assert.Equal("About us", sections[0].GetProperty("title").GetString());
        Assert.Equal("<p>Hello</p>", sections[0].GetProperty("content").GetString());
        Assert.Equal(image, sections[0].GetProperty("imageId").GetGuid());
        Assert.Equal($"/sites/images/{image}", sections[0].GetProperty("imageUrl").GetString());
        Assert.Equal("Team", sections[0].GetProperty("imageAlt").GetString());
        Assert.Equal("Right", sections[0].GetProperty("imagePosition").GetString());
        Assert.Equal("Normal", sections[1].GetProperty("theme").GetString());
        Assert.Equal(JsonValueKind.Null, sections[1].GetProperty("imageUrl").ValueKind);
    }

    [Fact]
    public async Task Create_ThenGetVersion_ReturnsTheSectionsForTheEditor()
    {
        var saved = await (await PostAsync(Body(null))).Content.ReadFromJsonAsync<JsonElement>();
        var id = saved.GetProperty("id").GetGuid();

        var version = await _client.GetFromJsonAsync<JsonElement>($"/sites/{id}/versions/1");

        Assert.Equal(2, version.GetProperty("sections").GetArrayLength());
    }

    [Fact]
    public async Task Create_WithAnUnknownTheme_IsRejected()
    {
        var response = await PostAsync(Body(null, "Neon"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithAnUnknownImage_IsRejectedAndSavesNothing()
    {
        var response = await PostAsync(Body(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/sites/slug/landing")).StatusCode);
    }

    [Fact]
    public async Task Create_WithoutSections_StillWorks()
    {
        var response = await PostAsync("""{"title":"Plain","publish":true,"content":"<p>x</p>"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await (await _client.GetAsync("/sites/slug/plain")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, page.GetProperty("sections").GetArrayLength());
    }
}
