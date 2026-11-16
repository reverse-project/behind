using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Behind.Common.Configuration;

public static class ReloadableConfigurationExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddReloadableJsonFile(string path, bool optional)
        {
            var source = new ResilientJsonConfigurationSource
            {
                Path = path,
                Optional = optional,
                ReloadOnChange = true,
                OnReloadFailed = builder.ReloadFailures.Report
            };
            source.ResolveFileProvider();
            builder.Configuration.Add(source);

            return builder;
        }

        private ConfigurationReloadFailures ReloadFailures
        {
            get
            {
                if (builder.Services
                        .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(ConfigurationReloadFailures))
                        ?.ImplementationInstance is ConfigurationReloadFailures existing)
                {
                    return existing;
                }

                var failures = new ConfigurationReloadFailures();
                builder.Services.AddSingleton(failures);
                builder.Services.AddHostedService<ConfigurationReloadFailureLogger>();

                return failures;
            }
        }
    }
}
