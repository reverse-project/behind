using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

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

        var body = Assert.IsType<JsonHttpResult<ModulesResponse>>(ModuleEndpoints.Get(catalog)).Value!;

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
    }

    [Fact]
    public void Get_WithoutModulesListsNothing()
    {
        var body = Assert.IsType<JsonHttpResult<ModulesResponse>>(ModuleEndpoints.Get(ModuleCatalog.Empty)).Value!;

        Assert.Empty(body.Modules);
    }

    [Fact]
    public void ModulesEndpoint_IsOpenToVisitorsWhoseAccessIsRestricted()
    {
        Assert.True(new PathString(ModuleEndpoints.Route).IsAlwaysOpen);
    }
}
