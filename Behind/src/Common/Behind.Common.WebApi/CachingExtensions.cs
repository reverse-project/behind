using Behind.Common.Configuration;
using Behind.Common.Converters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Behind.Common.WebApi;

public static class CachingExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddCaching()
        {
            OptionTypeConverterRegistration.Register<CachingKey>();
            var configuration = builder.Configuration.GetSection(CachingConfiguration.SectionName)
                .GetOptionAware<CachingConfiguration>() ?? new CachingConfiguration();
            if (configuration.Provider is not Memory and not RedisCompatible)
            {
                throw new InvalidOperationException("Caching:Provider must be Memory or RedisCompatible.");
            }

            builder.Services.AddHybridCache();
            if (configuration.Provider is RedisCompatible)
            {
                var connection = configuration.Key.Match<string?>(
                    () => null,
                    key => builder.Configuration.GetConnectionString(key.Value));
                if (string.IsNullOrWhiteSpace(connection))
                {
                    builder.Services.AddHostedService<MissingCachingConnectionWarning>();
                }
                else
                {
                    builder.Services.AddStackExchangeRedisCache(options =>
                    {
                        options.Configuration = connection;
                    });
                }
            }

            return builder;
        }
    }

    private sealed class MissingCachingConnectionWarning(ILogger<MissingCachingConnectionWarning> logger) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogWarning("Caching selects RedisCompatible without a configured ConnectionStrings entry; using memory caching only.");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
