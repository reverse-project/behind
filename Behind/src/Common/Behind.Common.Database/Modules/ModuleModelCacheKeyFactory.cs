using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Behind.Common.Database.Modules;

public sealed class ModuleModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) =>
        context is IModuleModelContext modules
            ? (context.GetType(), modules.Modules, designTime)
            : (context.GetType(), designTime);
}
