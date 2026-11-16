using System.Reflection;

namespace Behind.Common.Modules;

public sealed record ModuleDatabase(Assembly ConfigurationAssembly)
{
    public IReadOnlyList<Type> SoftDeletedEntities { get; init; } = [];

    public IReadOnlyList<Type> ContentRestrictedEntities { get; init; } = [];

    public IReadOnlyList<string> ContentRestrictionParentProperties { get; init; } = [];

    public IReadOnlyDictionary<string, string> ContentLocaleEditPolicies { get; init; } =
        new Dictionary<string, string>();

    public IReadOnlyList<string> ReadOnlyEntities { get; init; } = [];
}
