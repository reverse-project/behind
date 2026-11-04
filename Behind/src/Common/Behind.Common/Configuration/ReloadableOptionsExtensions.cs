using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Behind.Common.Configuration;

public static class ReloadableOptionsExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddReloadableOptions<T>(Func<IConfiguration, T> bind) where T : class =>
            services.AddReloadableOptions(null, bind);

        public IServiceCollection AddReloadableOptions<T>(IConfiguration? configuration, Func<IConfiguration, T> bind)
            where T : class
        {
            services.AddSingleton(provider => new ReloadableOption<T>(
                configuration ?? provider.GetRequiredService<IConfiguration>(),
                bind,
                exception => provider.GetService<ConfigurationReloadFailures>()?.Report(typeof(T).Name, exception)));
            services.AddSingleton<IOptions<T>>(provider => provider.GetRequiredService<ReloadableOption<T>>());
            services.AddSingleton<IOptionsSnapshot<T>>(provider => provider.GetRequiredService<ReloadableOption<T>>());
            services.AddSingleton<IOptionsMonitor<T>>(provider => provider.GetRequiredService<ReloadableOption<T>>());

            return services;
        }

        public IServiceCollection AddReloadableOptions<T>(string section, Func<T> fallback) where T : class =>
            services.AddReloadableOptions(configuration =>
                configuration.GetSection(section).GetOptionAware<T>() ?? fallback());

        public IServiceCollection AddOptionsCacheInvalidation<TSource, TTarget>()
            where TSource : class
            where TTarget : class =>
            services.AddHostedService<OptionsCacheInvalidationService<TSource, TTarget>>();
    }
}
