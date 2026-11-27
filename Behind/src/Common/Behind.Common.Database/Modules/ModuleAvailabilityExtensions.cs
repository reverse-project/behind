using Behind.Common.Modules;
using Microsoft.EntityFrameworkCore;

namespace Behind.Common.Database.Modules;

public static class ModuleAvailabilityExtensions
{
    extension(ModuleAvailability availability)
    {
        public bool IsEntityEnabled(ModuleCatalog catalog, string entityName) =>
            !catalog.Modules.Where(module => !availability.IsEnabled(module.Key))
                .SelectMany(module => module.Database.AsEnumerable())
                .SelectMany(database => database.ConfigurationAssembly.GetTypes())
                .SelectMany(type => type.GetInterfaces())
                .Where(type => type.IsGenericType
                    && type.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>))
                .Any(type => type.GenericTypeArguments[0].Name == entityName);
    }
}
