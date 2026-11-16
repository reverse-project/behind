using System.Reflection;

namespace Behind.Common.Modules;

public sealed record ModuleFrontend(Assembly Assembly, IReadOnlyList<string> Styles)
{
    public const string Root = "Frontend";

    public ModuleFrontend(Assembly assembly)
        : this(assembly, [])
    {
    }
}
