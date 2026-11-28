using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Behind.Common;
using Behind.Common.Modules;
using Behind.Common.WebApi.Mcp;
using Behind.Common.WebApi.Infrastructure.Localization;
using Behind.Common.WebApi.Infrastructure.StaticFiles;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.Domain;
using Behind.Core.WebApi;
using Behind.Core.WebApi.Authorization;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Mcp;
using Behind.Core.WebApi.Middleware;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Mcp;

public sealed class McpPipelineTests
{
    [Fact]
    public async Task HttpRequestsUseIndependentLiveAccessAndRevocableCredentials()
    {
        using var bed = new NewsTestBed();
        ServiceUserCredential credential;
        await using (var context = bed.Write())
        {
            await context.EnsureBuiltInRolesAsync(AccessCatalog.Core);
            credential = (await new ServiceUserStore(context, TimeProvider.System).Create("agent", default))
                .Match(() => throw new InvalidOperationException(), value => value);
        }

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<AbstractCoreContext>(_ => bed.Read());
        builder.Services.AddScoped<IServiceUserStore, ServiceUserStore>();
        builder.Services.AddScoped<IAccessQueries, AccessQueries>();
        builder.Services.AddScoped<IUserSettingQueries, UserSettingQueries>();
        builder.Services.AddSingleton(new LocaleCatalog(EmbeddedFrontend.CreateFileProvider(builder.Environment.WebRootPath)));
        builder.Services.AddScoped<SessionQueries>();
        builder.Services.AddScoped<ISessionQueries, McpSessionQueries>();
        builder.Services.AddScoped<IMcpServer, McpServer>();
        builder.Services.AddScoped<IModuleMcpToolCatalog, ModuleMcpToolCatalog>();
        builder.Services.AddSingleton(ModuleCatalog.Empty);
        builder.Services.AddSingleton<ModuleAvailability>();
        builder.Services.AddScoped<IMcpRequestDispatcher, McpRequestDispatcher>();
        builder.Services.AddOpenApi();
        var mcpOptions = Substitute.For<IOptionsMonitor<McpConfiguration>>();
        mcpOptions.CurrentValue.Returns(new McpConfiguration { Enabled = true, MaxResponseBytes = 1024 });
        var accountOptions = Substitute.For<IOptionsMonitor<AccountsConfiguration>>();
        accountOptions.CurrentValue.Returns(new AccountsConfiguration { ServiceUser = true });
        builder.Services.AddSingleton(mcpOptions);
        builder.Services.AddSingleton(accountOptions);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        builder.Services.AddAuthorization(options => options.AddPolicy("test-write",
            policy => policy.RequireAuthenticatedUser().AddRequirements(new GrantRequirement("News.Write"))));
        builder.Services.AddScoped<IAuthorizationHandler, GrantAuthorizationHandler>();
        await using var app = builder.Build();
        app.UseMiddleware<McpMiddleware>();
        app.UseRouting();
        app.UseAuthentication();
        app.UseSessionValidation();
        app.UseAuthorization();
        app.MapOpenApi();
        app.MapPost("/mcp", (HttpContext context, IMcpServer server, CancellationToken token) => server.Handle(context, token));
        var executed = 0;
        app.MapPost("/test/command", async (HttpContext context, ISessionQueries sessions) =>
        {
            Interlocked.Increment(ref executed);
            var current = (await context.FindCurrentSession(sessions))
                .Match(() => throw new InvalidOperationException(), value => value);
            return TypedResults.Json(new
            {
                userId = current.UserId,
                serviceId = context.User.FindFirst("service_user_id")!.Value,
                locale = context.Request.Headers["X-Content-Locale"].ToString(),
                query = context.Request.Query["page"].ToString(),
                authorization = context.Request.Headers.Authorization.ToString()
            });
        }).RequireAuthorization("test-write");
        app.MapGet("/test/large", () => TypedResults.Json(new { text = new string('x', 1025) }));
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential.ApiKey);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");
        client.DefaultRequestHeaders.Add("X-Content-Locale", "en-US");

        mcpOptions.CurrentValue.Returns(new McpConfiguration { Enabled = true });
        using (var schema = await client.PostAsJsonAsync("/mcp", new
        {
            jsonrpc = "2.0", id = "schema", method = "tools/call",
            @params = new { name = "api_schema" }
        }))
        {
            Assert.Equal(HttpStatusCode.OK, schema.StatusCode);
            using var json = JsonDocument.Parse(await schema.Content.ReadAsStringAsync());
            var result = json.RootElement.GetProperty("result");
            Assert.False(result.GetProperty("isError").GetBoolean());
            using var text = JsonDocument.Parse(result.GetProperty("content")[0].GetProperty("text").GetString()!);
            Assert.Equal(200, text.RootElement.GetProperty("status").GetInt32());
            using var document = JsonDocument.Parse(text.RootElement.GetProperty("body").GetString()!);
            Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/test/command", out _));
        }
        mcpOptions.CurrentValue.Returns(new McpConfiguration { Enabled = true, MaxResponseBytes = 1024 });

        var message = new
        {
            jsonrpc = "2.0", id = 1, method = "tools/call",
            @params = new { name = "api_request", arguments = new { method = "POST", path = "/test/command", query = new { page = "2" } } }
        };
        using (var denied = await client.PostAsJsonAsync("/mcp", message))
        {
            Assert.Equal(HttpStatusCode.OK, denied.StatusCode);
            using var json = JsonDocument.Parse(await denied.Content.ReadAsStringAsync());
            Assert.True(json.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
            Assert.Equal(0, executed);
        }

        await using (var context = bed.Write())
        {
            await new AccessCommands(context).SetUserAccess(credential.User.Id, [BuiltInAccess.Users.Id],
                new AccessSelection(["News.Write"], []), default);
        }
        using (var allowed = await client.PostAsJsonAsync("/mcp", message))
        {
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
            using var json = JsonDocument.Parse(await allowed.Content.ReadAsStringAsync());
            var result = json.RootElement.GetProperty("result");
            Assert.False(result.GetProperty("isError").GetBoolean());
            using var text = JsonDocument.Parse(result.GetProperty("content")[0].GetProperty("text").GetString()!);
            Assert.Equal(200, text.RootElement.GetProperty("status").GetInt32());
            using var body = JsonDocument.Parse(text.RootElement.GetProperty("body").GetString()!);
            Assert.Equal(credential.User.Id, body.RootElement.GetProperty("userId").GetGuid());
            Assert.NotEqual(bed.AuthorId, body.RootElement.GetProperty("userId").GetGuid());
            Assert.Equal(credential.User.Id.ToString(), body.RootElement.GetProperty("serviceId").GetString());
            Assert.Equal("en-US", body.RootElement.GetProperty("locale").GetString());
            Assert.Equal("2", body.RootElement.GetProperty("query").GetString());
            Assert.Equal("", body.RootElement.GetProperty("authorization").GetString());
            Assert.Equal(1, executed);
        }

        await using (var context = bed.Write())
        {
            await new AccessCommands(context).SetUserAccess(credential.User.Id, [BuiltInAccess.Users.Id],
                new AccessSelection(["News.Write"], ["News.Write"]), default);
        }
        using (var restricted = await client.PostAsJsonAsync("/mcp", message))
        {
            using var json = JsonDocument.Parse(await restricted.Content.ReadAsStringAsync());
            Assert.True(json.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
            Assert.Equal(1, executed);
        }
        using (var direct = await client.PostAsJsonAsync("/test/command", new { }))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, direct.StatusCode);
            Assert.Equal(1, executed);
        }

        client.DefaultRequestHeaders.Add("Origin", "https://unapproved.example");
        using (var origin = await client.PostAsJsonAsync("/mcp", message))
        {
            Assert.Equal(HttpStatusCode.Forbidden, origin.StatusCode);
            Assert.Equal(1, executed);
        }
        client.DefaultRequestHeaders.Remove("Origin");
        using (var oversized = await client.PostAsJsonAsync("/mcp", new
        {
            jsonrpc = "2.0", id = 2, method = "tools/call",
            @params = new { name = "api_request", arguments = new { method = "GET", path = "/test/large" } }
        }))
        {
            Assert.Equal(HttpStatusCode.OK, oversized.StatusCode);
            using var json = JsonDocument.Parse(await oversized.Content.ReadAsStringAsync());
            var result = json.RootElement.GetProperty("result");
            Assert.True(result.GetProperty("isError").GetBoolean());
            using var text = JsonDocument.Parse(result.GetProperty("content")[0].GetProperty("text").GetString()!);
            Assert.Equal(502, text.RootElement.GetProperty("status").GetInt32());
        }
        await using (var context = bed.Write())
        {
            Assert.False((await new ServiceUserStore(context, TimeProvider.System)
                .Rotate(credential.User.Id, credential.User.Version, default)).IsNone());
        }
        using (var revoked = await client.PostAsJsonAsync("/mcp", message))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
            Assert.Equal(1, executed);
        }
        using (var trailingSlash = await client.PostAsJsonAsync("/MCP/", message))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, trailingSlash.StatusCode);
            Assert.Equal(1, executed);
        }
        await app.StopAsync();
    }

    [Fact]
    public async Task DisabledTransportDoesNotAuthenticateOrInvokeThePipeline()
    {
        var options = Substitute.For<IOptionsMonitor<McpConfiguration>>();
        options.CurrentValue.Returns(new McpConfiguration());
        var accounts = Substitute.For<IOptionsMonitor<AccountsConfiguration>>();
        accounts.CurrentValue.Returns(new AccountsConfiguration { ServiceUser = true });
        var store = Substitute.For<IServiceUserStore>();
        var middleware = new McpMiddleware(_ => throw new InvalidOperationException(), NullLogger<McpMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/mcp";
        await middleware.InvokeAsync(context, store, options, accounts, TimeProvider.System);
        Assert.Equal(404, context.Response.StatusCode);
        await store.DidNotReceiveWithAnyArgs().Authenticate(default!, default);
    }
}
