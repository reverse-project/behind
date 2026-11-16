using Behind.Common.Configuration;
using Behind.Common.Database.Modules;
using Behind.Common.Storage;
using Behind.Core.Common.Configuration;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Behind.Core.Database;

public static class VfsServiceExtensions
{
    private static BackendConfiguration Bind(IConfiguration configuration)
    {
        var backend = configuration
            .GetSection(BackendConfiguration.SectionName)
            .GetOptionAware<BackendConfiguration>() ?? new BackendConfiguration();

        _ = backend.VfsRoot;

        return backend;
    }

    extension(IServiceCollection services)
    {
        public IServiceCollection AddVfsContentStore(IConfiguration configuration)
        {
            _ = Bind(configuration);

            services.AddReloadableOptions(configuration, Bind);
            services.AddTransient<IContentStore>(provider =>
                new DirectoryContentStore(provider.GetRequiredService<IOptions<BackendConfiguration>>().Value.VfsRoot));
            services.AddScoped<IVfsQueries, VfsQueries>();
            services.AddScoped<IVfsCommands, VfsCommands>();
            services.AddScoped<IModuleFiles, ModuleFiles>();

            return services;
        }
    }
}
