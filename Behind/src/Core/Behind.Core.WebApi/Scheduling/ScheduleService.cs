using Behind.Common;
using Behind.Common.Logging;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Scheduling;

public sealed class ScheduleService(
    IServiceScopeFactory scopes,
    IScheduleRunner runner,
    TimeProvider clock,
    IOptionsMonitor<SchedulerConfiguration> options,
    ILogger<ScheduleService> logger) : BackgroundService
{
    private readonly Dictionary<Guid, DateTimeOffset> _accepted = [];
    private DateTimeOffset _last = clock.GetUtcNow().ToScheduleMinute().AddMinutes(-1);

    public async Task<int> TickAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var minutes = _last.ScheduleMinutesUntil(now, options.CurrentValue.CatchUpMinutes);

        if (minutes.Count == 0)
        {
            return 0;
        }

        await using var scope = scopes.CreateAsyncScope();
        var schedules = await scope.ServiceProvider.GetRequiredService<IScheduleQueries>().Enabled(cancellationToken);
        var started = 0;
        var failed = false;

        foreach (var schedule in schedules)
        {
            var accepted = _accepted.GetValueOrDefault(schedule.Id.Key, DateTimeOffset.MinValue);
            var due = minutes.Where(minute => minute > accepted && schedule.Cron.Matches(minute)).ToList();

            if (due.Count == 0)
            {
                continue;
            }

            if (runner.IsRunning(schedule.Id.Key))
            {
                logger.LogWarning("Schedule {ScheduleId} is still running; the run at {Minute} is skipped", () => [schedule.Id.Key, due[^1]]);
                _accepted[schedule.Id.Key] = due[^1];
                continue;
            }

            try
            {
                var run = await runner.TryStart(schedule, due[^1], cancellationToken);
                _accepted[schedule.Id.Key] = due[^1];
                started += run.Match(() => 0, _ => 1);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed = true;
                logger.LogError(exception, "Schedule {ScheduleId} could not be started for {Minute}", () => [schedule.Id.Key, due[^1]]);
            }
        }

        // Failed minutes stay pending for the next tick; accepted starts are remembered so they are not repeated.
        if (!failed)
        {
            _last = minutes[^1];
            _accepted.Clear();
        }

        return started;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await runner.Stop().WaitAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.CurrentValue.Enabled)
        {
            logger.LogInformation("Scheduler is disabled");
        }

        await TryAsync(() => runner.RecoverInterrupted(stoppingToken), "Interrupted schedule runs could not be recovered");

        using var timer = new PeriodicTimer(options.CurrentValue.TickInterval, clock);

        do
        {
            if (options.CurrentValue.Enabled)
            {
                await TryAsync(() => TickAsync(stoppingToken), "Scheduler tick failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task TryAsync(Func<Task<int>> work, string failure)
    {
        try
        {
            await work();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, failure);
        }
    }
}
