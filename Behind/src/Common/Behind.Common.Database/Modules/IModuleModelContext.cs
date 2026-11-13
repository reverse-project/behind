using Behind.Common.Modules;

namespace Behind.Common.Database.Modules;

public interface IModuleModelContext
{
    ModuleCatalog Modules { get; }
}
