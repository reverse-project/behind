using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class RawDatabaseEndpointsHttpTests : IAsyncLifetime
{
    private readonly TestDbContextFactory _factory = new();
    private WebApplication _application = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddAuthorization(options =>
            options.AddPolicy(ManagedListExtensions.DeletedRecordsGrant, policy => policy.RequireAssertion(_ => true)));
        builder.Services.AddScoped<IAuditActorProvider>(_ => new FixedAuditActorProvider(AuditActor.Anonymous));
        builder.Services.AddScoped<AbstractCoreContext>(_ => _factory.CreateAuditedContext(AuditActor.Anonymous));
        builder.Services.AddScoped<IRawDatabaseQueries, RawDatabaseQueries>();
        builder.Services.AddScoped<IRawDatabaseCommands, RawDatabaseCommands>();

        _application = builder.Build();
        _application.UseRouting();
        _application.UseAuthorization();
        _application.MapRawDatabaseEndpoints();
        await _application.StartAsync();

        var address = _application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!
            .Addresses.First();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _application.DisposeAsync();
        _factory.Dispose();
    }

    private static JsonObject Body(params (string Name, JsonNode? Value)[] values) =>
        new(values.Select(pair => KeyValuePair.Create(pair.Name, pair.Value)));

    [Fact]
    public async Task Tables_ListsEveryTableWithItsColumns()
    {
        var response = await _client.GetFromJsonAsync<JsonNode>("/raw-database/tables");

        var grants = response!["tables"]!.AsArray().Single(table => (string)table!["name"]! == "Grants")!;
        Assert.Equal("Grant", (string)grants["entityName"]!);
        Assert.False((bool)grants["isReadOnly"]!);
        Assert.Equal(["Id", "Name"], grants["columns"]!.AsArray().Select(column => (string)column!["name"]!));
        Assert.True(response["tables"]!.AsArray().Single(table => (string)table!["name"]! == "AuditLogEntries")!["isReadOnly"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Rows_UnknownTable_Is404()
    {
        var response = await _client.GetAsync("/raw-database/tables/Nope/rows");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InsertUpdateListAndDelete_RoundTripThroughJson()
    {
        var insert = await _client.PostAsJsonAsync(
            "/raw-database/tables/Grants/rows",
            Body(("values", Body(("Name", "Http.Grant"))))); 
        Assert.Equal(HttpStatusCode.OK, insert.StatusCode);
        var inserted = (await insert.Content.ReadFromJsonAsync<JsonNode>())!;
        var id = (string)inserted["Id"]!;
        Assert.Equal("Http.Grant", (string)inserted["Name"]!);

        var update = await _client.PostAsJsonAsync(
            "/raw-database/tables/Grants/rows/update",
            Body(("key", Body(("Id", id))), ("values", Body(("Name", "Http.Renamed")))));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var page = (await _client.GetFromJsonAsync<JsonNode>("/raw-database/tables/Grants/rows?search=Http.Renamed&orderBy=Name"))!;
        Assert.Equal(1, (long)page["total"]!);
        Assert.Equal(id, (string)page["items"]![0]!["Id"]!);

        var delete = await _client.PostAsJsonAsync(
            "/raw-database/tables/Grants/rows/delete",
            Body(("key", Body(("Id", id)))));
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var gone = await _client.PostAsJsonAsync(
            "/raw-database/tables/Grants/rows/delete",
            Body(("key", Body(("Id", id)))));
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task Insert_UnknownColumn_Is400WithTheErrors()
    {
        var response = await _client.PostAsJsonAsync(
            "/raw-database/tables/Grants/rows",
            Body(("values", Body(("Nope", 1)))));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
        Assert.Contains("Nope", (string)body["errors"]![0]!);
    }

    [Fact]
    public async Task Insert_DuplicateUniqueValue_Is409Rejected()
    {
        var first = await _client.PostAsJsonAsync("/raw-database/tables/Grants/rows", Body(("values", Body(("Name", "Http.Unique")))));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/raw-database/tables/Grants/rows", Body(("values", Body(("Name", "Http.Unique")))));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<JsonNode>())!;
        Assert.Equal("rejected", (string)body["reason"]!);
        Assert.False(string.IsNullOrWhiteSpace((string)body["message"]!));
    }

    [Fact]
    public async Task Delete_FromTheAuditLog_Is409ReadOnly()
    {
        var response = await _client.PostAsJsonAsync(
            "/raw-database/tables/AuditLogEntries/rows/delete",
            Body(("key", Body(("Id", Guid.NewGuid().ToString())))));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("read-only", (string)(await response.Content.ReadFromJsonAsync<JsonNode>())!["reason"]!);
    }

    [Fact]
    public async Task Insert_WithoutValues_IsRejectedByValidation()
    {
        var response = await _client.PostAsJsonAsync("/raw-database/tables/Grants/rows", new JsonObject());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
