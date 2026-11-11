using static Behind.Common.FunctionalExtensions;

namespace Behind.Common.Modules;

public sealed record ModuleDescriptor(string Key, string Name)
{
    public IReadOnlyList<ModuleGrantArea> GrantAreas { get; init; } = [];

    public IReadOnlyList<ModuleMenuItem> MenuItems { get; init; } = [];

    public IReadOnlyList<ModuleAdministrationPage> AdministrationPages { get; init; } = [];

    public IReadOnlyList<ModuleRoute> Routes { get; init; } = [];

    public IReadOnlyList<ModuleWidget> Widgets { get; init; } = [];

    public IReadOnlyList<string> ConfigurationFiles { get; init; } = [];

    public Option<ModuleFrontend> Frontend { get; init; } = None;

    public Option<ModuleDatabase> Database { get; init; } = None;
}
