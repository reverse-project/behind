using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Behind.Common.Configuration;

public static class ReloadableConfigurationExtensions
{
    private const string FailuresKey = "Behind.ConfigurationReloadFailures";

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
                if (builder.Configuration.Properties.TryGetValue(FailuresKey, out var existing))
                {
                    return (ConfigurationReloadFailures)existing;
                }

                var failures = new ConfigurationReloadFailures();
                builder.Configuration.Properties[FailuresKey] = failures;
                builder.Services.AddSingleton(failures);
                builder.Services.AddHostedService<ConfigurationReloadFailureLogger>();

                return failures;
            }
        }
    }
}
