using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Behind.Common.WebApi.Mcp;
using Behind.Common.Configuration;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Domain;
using Behind.Core.Tools;
using Behind.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi.Mcp;

public sealed class ModuleMcpPipelineTests : IAsyncLifetime
{
    private readonly string directory = Directory.CreateTempSubdirectory("behind-module-mcp-").FullName;
    private WebApplication application = null!;
    private HttpClient client = null!;
    private Guid serviceId;

    public async Task InitializeAsync()
    {
        new StartupSetupRequest(Path.Combine(directory, "data"), directory, "issuer", "audience",
            false, "password", false, false).Run(TextWriter.Null);
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { ContentRootPath = directory });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.AddWeb();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Backend:Scheduler:Enabled"] = "false",
            ["Backend:Accounts:ServiceUser"] = "true",
            ["Backend:Mcp:Enabled"] = "true"
        });
        application = builder.Build().UseWeb();
        ServiceUserCredential credential;
        await using (var scope = application.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AbstractCoreContext>();
            await context.Database.EnsureCreatedAsync();
            await context.EnsureBuiltInRolesAsync(application.Services.GetRequiredService<AccessCatalog>());
            credential = (await scope.ServiceProvider.GetRequiredService<IServiceUserStore>().Create("module agent", default))
                .Match(() => throw new InvalidOperationException("Service user creation failed."), value => value);
        }
        serviceId = credential.User.Id;
        await application.StartAsync();
        client = new HttpClient { BaseAddress = new Uri(application.Urls.Single()) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential.ApiKey);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");
    }

    public async Task DisposeAsync()
    {
        client?.Dispose();
        if (application is not null)
        {
            await application.DisposeAsync();
        }
        Directory.Delete(directory, true);
    }

    private async Task<JsonObject> Send(string method, object parameters)
    {
        using var response = await client.PostAsJsonAsync("/mcp", new
        {
            jsonrpc = "2.0", id = 1, method, @params = parameters
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.True(JsonSchemas.IsValid("mcp-1.1.0", message), message.ToJsonString());
        return message;
    }

    private async Task<JsonObject> Call(string name, object arguments)
    {
        var message = await Send("tools/call", new { name, arguments });
        Assert.False(message.ContainsKey("error"), message.ToJsonString());
        return JsonNode.Parse(message["result"]!["content"]![0]!["text"]!.GetValue<string>())!.AsObject();
    }

    private async Task SetAccess(string[] grants, string[] restrictions)
    {
        await using var scope = application.Services.CreateAsyncScope();
        await new AccessCommands(scope.ServiceProvider.GetRequiredService<AbstractCoreContext>())
            .SetUserAccess(serviceId, [BuiltInAccess.Users.Id], new AccessSelection(grants, restrictions));
    }

    [Fact]
    public async Task OpenApiDiscoveryIncludesCoreAndModuleRequestSchemas()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/openapi/v1.json");
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.NotNull(document["paths"]!["/sign/up/confirm"]!["post"]!["requestBody"]);
        Assert.NotNull(document["paths"]!["/forum/posts"]!["post"]!["requestBody"]);

        var schema = await Call("api_schema", new { });
        Assert.Equal(200, schema["status"]!.GetValue<int>());
    }

    [Fact]
    public async Task EveryInstalledModuleHasTypedToolsAndWorkingReadsWithoutBinaryTransfers()
    {
        var message = await Send("tools/list", new { });
        var tools = message["result"]!["tools"]!.AsArray();
        var names = tools.Select(tool => tool!["name"]!.GetValue<string>()).ToArray();
        Assert.Equal(names.Length, names.Distinct().Count());
        Assert.Contains("api_schema", names);
        Assert.Contains("api_request", names);
        foreach (var name in new[] { "contact_get_forms", "forum_get_posts", "quotes_get",
            "sport_get_teams", "markdownviewer_get_documents" })
        {
            Assert.Contains(name, names);
            Assert.Equal(200, (await Call(name, new { }))["status"]!.GetValue<int>());
        }
        Assert.Contains("forum_get_manage_users", names);
        Assert.Contains("forum_post_manage_users", names);
        Assert.Contains("quotes_post_by_id_restore", names);
        Assert.Contains("sport_post_teams_by_id_hard_delete", names);
        Assert.DoesNotContain(names, name => name.Contains("_images", StringComparison.Ordinal));
        Assert.DoesNotContain("markdownviewer_get_files_by_path", names);

        var create = Assert.Single(tools, tool => tool!["name"]!.GetValue<string>() == "forum_post_posts")!;
        Assert.Equal("string", create["inputSchema"]!["properties"]!["body"]!["properties"]!["body"]!["type"]!.GetValue<string>());
        Assert.Contains("body", create["inputSchema"]!["required"]!.AsArray().Select(item => item!.GetValue<string>()));
        var manage = Assert.Single(tools, tool => tool!["name"]!.GetValue<string>() == "quotes_get_manage")!;
        Assert.Equal("boolean", manage["inputSchema"]!["properties"]!["query"]!["properties"]!["deleted"]!["type"]!.GetValue<string>());
        var read = Assert.Single(tools, tool => tool!["name"]!.GetValue<string>() == "forum_get_posts_by_id")!;
        Assert.Equal("string", read["inputSchema"]!["properties"]!["route"]!["properties"]!["id"]!["type"]!.GetValue<string>());
        var update = Assert.Single(tools, tool => tool!["name"]!.GetValue<string>() == "forum_post_boards_by_id")!;
        Assert.Contains("route", update["inputSchema"]!["required"]!.AsArray().Select(item => item!.GetValue<string>()));

        await using var scope = application.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IModuleMcpToolCatalog>().List();
        var expected = ((IEndpointRouteBuilder)application).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Where(endpoint => endpoint.Metadata.GetMetadata<ModuleEndpoint>() is not null
                && !endpoint.RoutePattern.RawText!.Contains("/images", StringComparison.Ordinal)
                && !endpoint.RoutePattern.RawText.StartsWith("/markdownviewer/files", StringComparison.Ordinal))
            .SelectMany(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Select(method =>
                method + " " + Regex.Replace(endpoint.RoutePattern.RawText!, @"\{(?:\*\*)?([^}:?]+)(?::[^}]+)?\}",
                    "{$1}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))));
        Assert.Equal(expected.Order(StringComparer.Ordinal),
            catalog.Select(tool => tool.Method + " " + tool.Path).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ModuleMutationsEnforceLiveGrantsAndRestrictionsAndExecuteOnce()
    {
        var body = new { text = "An agent-created quote", author = "Agent" };
        Assert.Equal(403, (await Call("quotes_post", new { body }))["status"]!.GetValue<int>());
        await SetAccess(["Quotes.Write", "Quotes.Delete"], []);
        var created = await Call("quotes_post", new { body });
        Assert.Equal(200, created["status"]!.GetValue<int>());
        var id = JsonNode.Parse(created["body"]!.GetValue<string>())!["id"]!.GetValue<Guid>();
        var listed = await Call("quotes_get", new { query = new { page = 1, pageSize = 10 } });
        var page = JsonNode.Parse(listed["body"]!.GetValue<string>())!["quotes"]!;
        Assert.Equal(1, page["total"]!.GetValue<int>());

        await SetAccess(["Quotes.Write", "Quotes.Delete"], ["Quotes.Write"]);
        Assert.Equal(403, (await Call("quotes_post", new { body }))["status"]!.GetValue<int>());
        Assert.Equal(204, (await Call("quotes_post_by_id_delete", new { route = new { id = id.ToString() } }))["status"]!.GetValue<int>());
        var empty = await Call("quotes_get", new { });
        Assert.Equal(0, JsonNode.Parse(empty["body"]!.GetValue<string>())!["quotes"]!["total"]!.GetValue<int>());

        await SetAccess(["Forum.Manage"], []);
        var board = await Call("forum_post_boards", new { body = new { name = "Agents" } });
        Assert.Equal(200, board["status"]!.GetValue<int>());
        var boardId = JsonNode.Parse(board["body"]!.GetValue<string>())!["id"]!.GetValue<Guid>();
        var post = await Call("forum_post_posts", new { body = new { boardId, title = "Hello", body = "Posted by the service user." } });
        Assert.Equal(201, post["status"]!.GetValue<int>());
        var postId = JsonNode.Parse(post["body"]!.GetValue<string>())!["id"]!.GetValue<Guid>();
        var read = await Call("forum_get_posts_by_id", new { route = new { id = postId.ToString() } });
        Assert.Equal(200, read["status"]!.GetValue<int>());
        Assert.Equal(serviceId, JsonNode.Parse(read["body"]!.GetValue<string>())!["authorId"]!.GetValue<Guid>());
    }

    [Fact]
    public async Task DisabledModulesDisappearAndInvalidArgumentsCannotRetargetOperations()
    {
        application.Configuration["Backend:Modules:quotes"] = "false";
        var tools = (await Send("tools/list", new { }))["result"]!["tools"]!.AsArray();
        Assert.DoesNotContain(tools, tool => tool!["name"]!.GetValue<string>().StartsWith("quotes_", StringComparison.Ordinal));
        var disabled = await Send("tools/call", new { name = "quotes_get", arguments = new { } });
        Assert.Equal(-32602, disabled["error"]!["code"]!.GetValue<int>());
        application.Configuration["Backend:Modules:quotes"] = "true";
        Assert.Equal(200, (await Call("quotes_get", new { }))["status"]!.GetValue<int>());
        foreach (var arguments in new object[]
        {
            new { },
            new { route = new { id = "../mcp" }, body = new { text = "x", author = "x" } },
            new { route = new { id = "/service-users" }, body = new { text = "x", author = "x" } },
            new { route = new { id = "{id}" }, body = new { text = "x", author = "x" } },
            new { route = new { id = Guid.NewGuid().ToString() }, method = "DELETE", path = "/users" }
        })
        {
            var invalid = await Send("tools/call", new { name = "quotes_post_by_id", arguments });
            Assert.Equal(-32602, invalid["error"]!["code"]!.GetValue<int>());
        }
    }
}
