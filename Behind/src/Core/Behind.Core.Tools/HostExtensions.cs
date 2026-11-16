using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Behind.Core.Tools;

public static class HostExtensions
{
    extension(HostApplicationBuilder builder)
    {
        public HostApplicationBuilder AddTools()
        {
            builder.Services.AddHostedService<CommandLineService>();
            builder.Logging.ClearProviders();
            
            return builder;
        }
    }
}