using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Behind.Common.Configuration;

public sealed class OptionsCacheInvalidationService<TSource, TTarget>(
    IOptionsMonitor<TSource> source,
    IOptionsMonitorCache<TTarget> cache) : IHostedService
    where TSource : class
    where TTarget : class
{
    private IDisposable? _subscription;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = source.OnChange(_ => cache.Clear());

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();

        return Task.CompletedTask;
    }
}
