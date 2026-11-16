using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Behind.Common.Configuration;

public sealed class ConfigurationReloadFailureLogger(
    ConfigurationReloadFailures failures,
    ILogger<ConfigurationReloadFailureLogger> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        failures.Failed += Log;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        failures.Failed -= Log;

        return Task.CompletedTask;
    }

    private void Log(string source, Exception exception) =>
        logger.LogError(exception, "Reloading the configuration of {Source} failed, the previous values stay active", source);
}
