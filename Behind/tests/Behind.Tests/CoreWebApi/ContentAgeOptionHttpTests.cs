using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi;

public sealed class ContentAgeOptionHttpTests : IAsyncLifetime
{
    private readonly TestDbContextFactory _factory = new();
    private WebApplication _application = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddAuthorization(options =>
            options.AddPolicy("ContentAge.Manage", policy => policy.RequireAssertion(_ => true)));
        builder.Services.AddScoped<AbstractCoreContext>(_ => _factory.CreateAuditedContext(AuditActor.Anonymous));
        _application = builder.Build();
        _application.UseAuthorization();
        _application.MapContentAgeOptionEndpoints();
        await _application.StartAsync();
        var address = _application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _application.DisposeAsync();
        _factory.Dispose();
    }

    [Fact]
    public async Task CreateAndUpdateReturnTrimmedNamesAndRejectInvalidRequests()
    {
        var invalid = await _client.PostAsJsonAsync("/content-age-options", new { age = 18 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var created = await _client.PostAsJsonAsync("/content-age-options", new { age = 18, name = "  Eighteen  " });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = (await created.Content.ReadFromJsonAsync<JsonNode>())!;
        var id = body["id"]!.GetValue<Guid>();
        Assert.Equal(18, body["age"]!.GetValue<int>());
        Assert.Equal("Eighteen", body["name"]!.GetValue<string>());
        var rejected = await _client.PostAsJsonAsync($"/content-age-options/{id}/update", new { name = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var updated = await _client.PostAsJsonAsync($"/content-age-options/{id}/update", new { name = "  Adults  ", age = 12 });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var result = (await updated.Content.ReadFromJsonAsync<JsonNode>())!;
        Assert.Equal(id, result["id"]!.GetValue<Guid>());
        Assert.Equal(18, result["age"]!.GetValue<int>());
        Assert.Equal("Adults", result["name"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsync($"/content-age-options/{id}/delete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.PostAsJsonAsync($"/content-age-options/{id}/update", new { name = "Again" })).StatusCode);
    }
}
