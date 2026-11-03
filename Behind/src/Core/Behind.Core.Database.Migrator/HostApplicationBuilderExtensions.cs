using Behind.Common.Configuration;
using Behind.Common.Logging;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Behind.Core.Database.Migrator;

public static class HostApplicationBuilderExtensions
{
    extension(HostApplicationBuilder builder)
    {
        public HostApplicationBuilder AddMigrator() =>
            builder
                .AddSerilog()
                .AddConfiguration()
                .AddHostedServices();
        
        HostApplicationBuilder AddConfiguration()
        {
            builder.Configuration.AddJsonFile("database.json", optional: false, reloadOnChange: true);

            builder.Configuration
                .GetSection(DatabaseConfiguration.SectionName)
                .GetOptionAware<DatabaseConfiguration>()
                ?.Register(builder);

            builder.Configuration.AddJsonFile("backend.json", optional: false, reloadOnChange: true);
            builder.Services.AddVfsContentStore(builder.Configuration);

            return builder;
        }
        HostApplicationBuilder AddHostedServices()
        {
            builder.Services.AddHostedService<MigratorService>();
            
            return builder;
        }
    }
}