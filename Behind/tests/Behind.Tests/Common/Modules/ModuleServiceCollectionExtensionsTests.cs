using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.Common.Modules;

public class ModuleServiceCollectionExtensionsTests
{
    [Fact]
    public void AddModule_ResolvesACatalogOfEveryRegisteredModule()
    {
        var other = new ModuleDescriptor("other", "Other");
        var services = new ServiceCollection().AddModule(NotesModule.Descriptor).AddModule(other);

        using var provider = services.BuildServiceProvider();

        Assert.Equal(
            ["notes", "other"],
            provider.GetRequiredService<ModuleCatalog>().Modules.Select(module => module.Key));
    }

    [Fact]
    public void AddModule_IgnoresTheSameModuleRegisteredTwice()
    {
        var services = new ServiceCollection().AddModule(NotesModule.Descriptor).AddModule(NotesModule.Descriptor);

        using var provider = services.BuildServiceProvider();

        Assert.Single(provider.GetRequiredService<ModuleCatalog>().Modules);
    }

    [Fact]
    public void AddModuleCatalog_ResolvesAnEmptyCatalogWithoutModules()
    {
        using var provider = new ServiceCollection().AddModuleCatalog().BuildServiceProvider();

        Assert.Empty(provider.GetRequiredService<ModuleCatalog>().Modules);
    }

    [Fact]
    public void Catalog_FailsFastWhenAModuleIsInvalid()
    {
        using var provider = new ServiceCollection()
            .AddModule(new ModuleDescriptor("Bad Key", "Bad"))
            .BuildServiceProvider();

        var failure = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ModuleCatalog>());

        Assert.Contains("Invalid modules", failure.Message);
    }
}
