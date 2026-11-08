using Behind.Core.WebApi;
using Behind.Core.WebApi.Settings;
using Behind.MarkdownViewer.Common;
using Behind.MarkdownViewer.WebApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.MarkdownViewer;

public sealed class MarkdownViewerModuleTests : IDisposable
{
    private readonly MarkdownSourceTestBed _bed = new MarkdownSourceTestBed().With("README.md", "# Home");

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        _bed.Dispose();
    }

    private WebApplication BuildApplication(string markdownViewerJson)
    {
        var root = _bed.Root;
        File.WriteAllText(
            Path.Combine(root, "database.json"),
            $$"""
              {
                "ConnectionStrings": { "Default": "Data Source={{Path.Combine(root, "test.db")}}" },
                "Database": { "Provider": "Sqlite", "Key": "Default" }
              }
              """);
        File.WriteAllText(Path.Combine(root, "email.json"), "{}");
        File.WriteAllText(
            Path.Combine(root, "backend.json"),
            $$"""{ "Backend": { "DataDirectory": "{{Path.Combine(root, "data").Replace("\\", "\\\\")}}" } }""");
        File.WriteAllText(Path.Combine(root, MarkdownViewerModuleNames.ConfigurationFile), markdownViewerJson);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root });
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, "http://127.0.0.1:0");
        builder.AddCore().AddMarkdownViewer();

        return builder.Build();
    }

    [Fact]
    public void Descriptor_IsValid()
    {
        Assert.True(ModuleCatalog.Create([MarkdownViewerModule.Descriptor]).IsValid);
    }

    [Fact]
    public void Descriptor_DeclaresTheConfigurationFileAndTheGrantArea()
    {
        Assert.Equal([MarkdownViewerModuleNames.ConfigurationFile], MarkdownViewerModule.Descriptor.ConfigurationFiles);
        Assert.Equal(
            [MarkdownViewerModuleNames.Area],
            MarkdownViewerModule.Descriptor.GrantAreas.Select(area => area.Area));
        Assert.Equal(None, MarkdownViewerModule.Descriptor.Database);
    }

    [Fact]
    public void Descriptor_LinksTheMenuAndTheAdministrationPage()
    {
        var descriptor = MarkdownViewerModule.Descriptor;

        Assert.Equal(MarkdownViewerModuleNames.PublicRoute, descriptor.MenuItems.Single().Url);
        Assert.Equal(MarkdownViewerModuleNames.ManageRoute, descriptor.AdministrationPages.Single().Href);
        Assert.Equal(MarkdownViewerModuleNames.WriteGrant, descriptor.AdministrationPages.Single().Grant);
        Assert.Contains(descriptor.Routes, route => route.Path == MarkdownViewerModuleNames.ManageRoute);
    }

    [Fact]
    public async Task AddMarkdownViewer_RegistersTheModuleAndItsSettingsFile()
    {
        await using var app = BuildApplication(
            $$"""{ "MarkdownViewer": { "SourceDirectory": "{{_bed.Source.Replace("\\", "\\\\")}}", "Title": "Guide" } }""");

        var catalog = app.Services.GetRequiredService<ModuleCatalog>();
        var files = app.Services.GetRequiredService<ConfigurationFileCatalog>();

        Assert.Contains(MarkdownViewerModule.Descriptor, catalog.Modules);
        Assert.Contains(MarkdownViewerModuleNames.ConfigurationFile, files.Names);
        Assert.Contains(MarkdownViewerModuleNames.ConfigurationFile, files.Files.Select(file => file.Name));
    }

    [Fact]
    public async Task AddMarkdownViewer_ReadsTheDirectoriesFromTheConfigurationFile()
    {
        await using var app = BuildApplication(
            $$"""
              {
                "MarkdownViewer": {
                  "SourceDirectory": "{{_bed.Source.Replace("\\", "\\\\")}}",
                  "OutputDirectory": "site",
                  "Title": "Guide"
                }
              }
              """);

        var generation = app.Services.GetRequiredService<MarkdownWorkspaceProvider>().Generation
            .GetOrElse(() => throw new InvalidOperationException());

        Assert.Equal(_bed.Source, generation.Workspace.Source.FullName);
        Assert.Equal(Path.Combine(_bed.Root, "site"), generation.Output.FullName);
        Assert.Equal("Guide", generation.Workspace.Title);
    }

    [Fact]
    public async Task AddMarkdownViewer_WithoutDirectories_IsNotConfigured()
    {
        await using var app = BuildApplication("{}");

        var provider = app.Services.GetRequiredService<MarkdownWorkspaceProvider>();

        Assert.False(provider.Viewing.IsValid);
        Assert.False(provider.Generation.IsValid);
    }
}
