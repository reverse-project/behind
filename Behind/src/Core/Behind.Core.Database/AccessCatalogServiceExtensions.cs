using Behind.Common.Modules;
using Behind.Core.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Behind.Core.Database;

public static class AccessCatalogServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAccessCatalog()
        {
            services.AddModuleCatalog();
            services.TryAddSingleton(provider =>
                AccessCatalog.From(provider.GetRequiredService<ModuleCatalog>()).Match(
                    errors => throw new InvalidOperationException($"Invalid modules: {string.Join("; ", errors)}"),
                    catalog => catalog));

            return services;
        }
    }
}
