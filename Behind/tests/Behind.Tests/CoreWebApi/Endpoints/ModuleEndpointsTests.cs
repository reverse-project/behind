using System.Text.Json;
using Behind.Common.Configuration;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Middleware;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class ModuleEndpointsTests
{
    [Fact]
    public void Get_ListsTheModulesWithTheirRoutesAdministrationPagesAndStyles()
    {
        var module = NotesModule.Descriptor with
        {
            Frontend = new ModuleFrontend(typeof(NotesModule).Assembly, ["modules/notes/css/notes.css"])
        };
        var catalog = ModuleCatalog.Create([module]).GetOrElse(ModuleCatalog.Empty);

        var body = Assert.IsType<JsonHttpResult<ModulesResponse>>(ModuleEndpoints.Get(
            catalog, AccessCatalog.From(catalog).GetOrElse(AccessCatalog.Core),
            new ModuleAvailability(new ConfigurationBuilder().Build()), new DefaultHttpContext())).Value!;

        var notes = Assert.Single(body.Modules);
        Assert.Equal("notes", notes.Key);
        Assert.Equal("Notes", notes.Name);
        Assert.Equal("modules/notes/", notes.Path);
        Assert.Equal(
            [
                new ModuleRouteResponse("#/notes", "modules/notes/controllers/notes", false),
                new ModuleRouteResponse("#/manage/notes", "modules/notes/controllers/notemanager", true)
            ],
            notes.Routes);
        Assert.Equal(
            [new ModuleAdministrationPageResponse("notes.administration", "fa-note-sticky", "#/manage/notes", "Notes.Write")],
            notes.AdministrationPages);
        Assert.Equal(["modules/notes/css/notes.css"], notes.Styles);
        Assert.True(JsonSchemas.IsValid("module-discovery-1.1.0",
            JsonSerializer.SerializeToNode(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
    }

    [Fact]
    public void Get_WithoutModulesListsNothing()
    {
        var body = Assert.IsType<JsonHttpResult<ModulesResponse>>(ModuleEndpoints.Get(
            ModuleCatalog.Empty, AccessCatalog.Core, new ModuleAvailability(new ConfigurationBuilder().Build()), new DefaultHttpContext())).Value!;

        Assert.Empty(body.Modules);
    }

    [Fact]
    public void AvailableGrantsFollowModuleSwitchesWithoutChangingTheInstalledCatalog()
    {
        var catalog = NotesModule.Catalog;
        var access = AccessCatalog.From(catalog).GetOrElse(AccessCatalog.Core);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var availability = new ModuleAvailability(configuration);

        ModulesResponse Read() => Assert.IsType<JsonHttpResult<ModulesResponse>>(
            ModuleEndpoints.Get(catalog, access, availability, new DefaultHttpContext())).Value!;

        Assert.Contains("Notes.Write", Read().AvailableGrants);
        configuration["Backend:Modules:notes"] = "false";
        var disabled = Read();
        Assert.DoesNotContain("Notes.Write", disabled.AvailableGrants);
        Assert.Contains("User.Manage", disabled.AvailableGrants);
        Assert.Contains("Notes.Write", access.GrantNames);
        configuration["Backend:Modules:notes"] = "true";
        Assert.Contains("Notes.Write", Read().AvailableGrants);
    }

    [Fact]
    public void ModulesEndpoint_IsOpenToVisitorsWhoseAccessIsRestricted()
    {
        Assert.True(new PathString(ModuleEndpoints.Route).IsAlwaysOpen);
    }
}
