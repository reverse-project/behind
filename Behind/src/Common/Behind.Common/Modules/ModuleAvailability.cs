using Microsoft.Extensions.Configuration;

namespace Behind.Common.Modules;

public sealed class ModuleAvailability(IConfiguration configuration)
{
    public const string ConfigurationPath = "Backend:Modules";
    public const string RequiredModule = "core";

    public bool IsEnabled(string key) =>
        key == RequiredModule || configuration.GetValue($"{ConfigurationPath}:{key}", true);

    public ModuleCatalog Active(ModuleCatalog catalog) =>
        new([..catalog.Modules.Where(module => IsEnabled(module.Key))]);
}
