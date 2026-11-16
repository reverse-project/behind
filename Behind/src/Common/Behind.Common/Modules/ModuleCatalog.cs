namespace Behind.Common.Modules;

public sealed record ModuleCatalog(IReadOnlyList<ModuleDescriptor> Modules)
{
    public static ModuleCatalog Empty { get; } = new([]);
}
