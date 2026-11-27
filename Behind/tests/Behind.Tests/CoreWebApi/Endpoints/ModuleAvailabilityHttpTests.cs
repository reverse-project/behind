using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Tools;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class ModuleAvailabilityHttpTests : IAsyncLifetime
{
    private readonly string directory = Directory.CreateTempSubdirectory("behind-module-switches-").FullName;
    private WebApplication application = null!;
    private HttpClient client = null!;

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
            ["Backend:Modules:contact"] = "false",
            ["Backend:Modules:quotes"] = "false",
            ["Backend:Modules:sport"] = "false",
            ["Backend:Modules:forum"] = "false",
            ["Backend:Modules:markdownviewer"] = "false"
        });
        application = builder.Build().UseWeb();
        await using (var scope = application.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AbstractCoreContext>().Database.EnsureCreatedAsync();
        }
        await application.StartAsync();
        var address = application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        client = new HttpClient { BaseAddress = new Uri(address) };
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

    [Fact]
    public async Task EveryModuleEndpointAndFrontendAssetIsBlockedAndCoreRemainsAvailable()
    {
        var routes = ((IEndpointRouteBuilder)application).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Where(endpoint => endpoint.Metadata.GetMetadata<ModuleEndpoint>() is not null).ToArray();
        Assert.Equal(["contact", "forum", "markdownviewer", "quotes", "sport"],
            routes.Select(route => route.Metadata.GetMetadata<ModuleEndpoint>()!.Key).Distinct().Order());

        foreach (var route in routes)
        {
            var path = Regex.Replace(route.RoutePattern.RawText!, @"\{[^}]+\}", match =>
                match.Value.Contains(":guid", StringComparison.OrdinalIgnoreCase)
                    ? "11111111-1111-1111-1111-111111111111"
                    : match.Value.Contains(":int", StringComparison.OrdinalIgnoreCase) ? "1" : "test",
                RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            foreach (var method in route.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)
            {
                using var request = new HttpRequestMessage(new HttpMethod(method), path);
                using var response = await client.SendAsync(request);
                Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{method} {path}: {response.StatusCode}");
            }
        }

        using var asset = await client.GetAsync("/modules/forum/locales/en-US.json");
        Assert.Equal(HttpStatusCode.NotFound, asset.StatusCode);
        var modules = await client.GetFromJsonAsync<ModulesResponse>("/modules");
        Assert.Equal("core", Assert.Single(modules!.Modules).Key);
        Assert.DoesNotContain("Forum.Read", modules.AvailableGrants);
        using var core = await client.GetAsync("/index.html");
        Assert.Equal(HttpStatusCode.OK, core.StatusCode);

        application.Configuration["Backend:Modules:forum"] = "true";
        modules = await client.GetFromJsonAsync<ModulesResponse>("/modules");
        Assert.Equal(["core", "forum"], modules!.Modules.Select(module => module.Key).Order());
        Assert.Contains("Forum.Read", modules.AvailableGrants);
        using var restored = await client.GetAsync("/modules/forum/locales/en-US.json");
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
    }
}
