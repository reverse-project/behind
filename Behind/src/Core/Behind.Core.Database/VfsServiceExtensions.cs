using Behind.Common.Configuration;
using Behind.Common.Storage;
using Behind.Core.Common.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Core.Database;

public static class VfsServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddVfsContentStore(IConfiguration configuration)
        {
            var backend = configuration
                .GetSection(BackendConfiguration.SectionName)
                .GetOptionAware<BackendConfiguration>() ?? new BackendConfiguration();

            services.AddSingleton<IContentStore>(new DirectoryContentStore(backend.VfsRoot));

            return services;
        }
    }
}
