using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Behind.Common.Modules;

public static class ModuleServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddModule(ModuleDescriptor module)
        {
            if (services.Any(descriptor =>
                    descriptor.ServiceType == typeof(ModuleDescriptor) && Equals(descriptor.ImplementationInstance, module)))
            {
                return services;
            }

            services.AddSingleton(module);

            return services.AddModuleCatalog();
        }

        public IServiceCollection AddModuleCatalog()
        {
            services.TryAddSingleton(ResolveCatalog);

            return services;
        }
    }

    private static ModuleCatalog ResolveCatalog(IServiceProvider services) =>
        ModuleCatalog.Create(services.GetServices<ModuleDescriptor>()).Match(
            errors => throw new InvalidOperationException(
                $"Invalid modules: {string.Join("; ", errors)}"),
            catalog => catalog);
}
