using Behind.Common.WebApi.Middleware;
using Behind.Common.Database.Raw;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Filters;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Behind.Tests.CommonWebApi;

public sealed class ModuleAvailabilityTests
{
    private static readonly ModuleCatalog Catalog = new([
        new("core", "Core"),
        new("notes", "Notes")
        {
            Routes = [new("#/notes/:id", "modules/notes/controllers/notes")],
            Widgets = [new("notes.feed", "notes.feed", "modules/notes/models/feed")]
        }
    ]);

    [Fact]
    public void MissingSwitchesDefaultToEnabledAndCoreCannotBeDisabled()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var availability = new ModuleAvailability(configuration);
        Assert.Equal(2, availability.Active(Catalog).Modules.Count);
        configuration["Backend:Modules:core"] = "false";
        configuration["Backend:Modules:notes"] = "false";
        Assert.Equal("core", Assert.Single(availability.Active(Catalog).Modules).Key);
        Assert.True(availability.IsEnabled("core"));
        configuration["Backend:Modules:notes"] = "true";
        Assert.Equal(2, availability.Active(Catalog).Modules.Count);
    }

    [Fact]
    public void DisabledModulesAreOmittedFromDiscoveryAndLocalizedLinksAndWidgets()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Backend:Modules:notes"] = "false" }).Build();
        var availability = new ModuleAvailability(configuration);
        var http = new DefaultHttpContext();
        var result = Assert.IsType<JsonHttpResult<ModulesResponse>>(ModuleEndpoints.Get(Catalog, AccessCatalog.Core, availability, http));
        Assert.Equal("core", Assert.Single(result.Value!.Modules).Key);
        Assert.Equal("no-store", http.Response.Headers.CacheControl);
        Assert.False(availability.IsRouteEnabled(Catalog, "#/notes/123?view=public"));
        Assert.False(availability.IsRouteEnabled(Catalog, "#/hu-HU/notes/123"));
        Assert.True(availability.IsRouteEnabled(Catalog, "#/news/123"));
        Assert.False(availability.IsWidgetEnabled(Catalog, "notes.feed"));
        Assert.True(availability.IsWidgetEnabled(Catalog, "core.news"));
    }

    [Theory]
    [InlineData("/notes/images/123", true)]
    [InlineData("/modules/notes/models/feed.js", false)]
    [InlineData("/modules/notes/locales/en-US.json", false)]
    [InlineData("/modules/notes/css/notes.css", false)]
    public async Task DisabledModuleRequestsNeverReachTheHandler(string path, bool endpointMetadata)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Backend:Modules:notes"] = "false" }).Build();
        var http = new DefaultHttpContext();
        http.Request.Path = path;
        if (endpointMetadata)
        {
            http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new ModuleEndpoint("notes")), "notes"));
        }
        var executed = false;
        var middleware = new ModuleAvailabilityMiddleware(_ =>
        {
            executed = true;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(http, new ModuleAvailability(configuration), Catalog, configuration,
            NullLogger<ModuleAvailabilityMiddleware>.Instance);
        Assert.False(executed);
        Assert.Equal(404, http.Response.StatusCode);
    }

    [Fact]
    public async Task DisabledModuleRawTablesCannotReachTheReaderOrWriter()
    {
        using var bed = new NotesTestBed();
        await using var context = bed.CreateContext();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Backend:Modules:notes"] = "false" }).Build();
        var http = new DefaultHttpContext();
        http.Request.RouteValues["table"] = context.RawTables.Single(table => table.EntityName == nameof(Note)).Name;
        var filter = new ModuleRawDatabaseFilter(new ModuleAvailability(configuration), NotesModule.Catalog,
            context, NullLogger<ModuleRawDatabaseFilter>.Instance);
        var executed = false;
        var result = await filter.InvokeAsync(EndpointFilterInvocationContext.Create(http), _ =>
        {
            executed = true;
            return ValueTask.FromResult<object?>(Results.NoContent());
        });
        Assert.IsType<NotFound>(result);
        Assert.False(executed);
    }

    [Fact]
    public async Task DisablingAModuleCancelsActiveRequestsAndRestoresTheOriginalToken()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var http = new DefaultHttpContext();
        using var original = new CancellationTokenSource();
        http.RequestAborted = original.Token;
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new ModuleEndpoint("notes")), "notes"));
        var middleware = new ModuleAvailabilityMiddleware(context =>
        {
            Assert.False(context.RequestAborted.IsCancellationRequested);
            configuration["Backend:Modules:notes"] = "false";
            configuration.Reload();
            Assert.True(context.RequestAborted.IsCancellationRequested);
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(http, new ModuleAvailability(configuration), Catalog, configuration,
            NullLogger<ModuleAvailabilityMiddleware>.Instance);
        Assert.Equal(original.Token, http.RequestAborted);
        Assert.False(original.IsCancellationRequested);
    }
}
