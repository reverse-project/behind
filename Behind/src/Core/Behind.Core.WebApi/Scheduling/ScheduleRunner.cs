using System.Collections.Concurrent;
using Behind.Common;
using Behind.Common.Logging;
using Behind.Common.Database;
using Behind.Common.WebApi;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Scheduling;

public sealed class ScheduleRunner(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    IOptionsMonitor<SchedulerConfiguration> options,
    ILogger<ScheduleRunner> logger) : IScheduleRunner, IDisposable
{
    public const string UnknownActionSummary = "Unknown scheduled action";
    public const string TimedOutSummary = "The scheduled action timed out";
    public const string InterruptedSummary = "The scheduled run was interrupted";
    public const string FailedSummaryPrefix = "The scheduled action failed: ";

    private readonly ConcurrentDictionary<Guid, Guid> _running = new();
    private readonly ConcurrentDictionary<Guid, Task> _tasks = new();
    private readonly SemaphoreSlim _slots = new(options.CurrentValue.ConcurrencyLimit);
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConcurrentDictionary<Guid, Task> _admissions = new();
    private readonly Lock _gate = new();
    private bool _closed;

    public bool IsRunning(Guid scheduleId) => _running.ContainsKey(scheduleId);

    public async Task<Option<Guid>> TryStart(
        IScheduleQueries.ScheduleDetails schedule,
        DateTimeOffset scheduledFor,
        CancellationToken cancellationToken = default)
    {
        var scheduleId = schedule.Id.Key;
        var admissionId = Guid.NewGuid();
        var admission = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
        {
            if (_closed || !_running.TryAdd(scheduleId, Guid.Empty))
            {
                return None;
            }

            _admissions[admissionId] = admission.Task;
        }

        try
        {
            var runId = await Record(schedule, scheduledFor, cancellationToken);
            _running[scheduleId] = runId;

            using (ExecutionContext.SuppressFlow())
            {
                var task = Task.Run(() => Execute(schedule, runId, scheduledFor), CancellationToken.None);
                _tasks[runId] = task;
                _ = task.ContinueWith(_ => _tasks.TryRemove(runId, out var _), TaskScheduler.Default);
            }

            return Some(runId);
        }
        catch
        {
            _running.TryRemove(scheduleId, out _);
            throw;
        }
        finally
        {
            _admissions.TryRemove(admissionId, out _);
            admission.SetResult();
        }
    }

    public async Task WhenIdle()
    {
        while (!_tasks.IsEmpty)
        {
            await Task.WhenAll(_tasks.Values.ToList());
        }
    }

    public async Task<int> RecoverInterrupted(CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IScheduleRunQueries>();
        var commands = scope.ServiceProvider.GetRequiredService<IScheduleRunCommands>();
        var active = _running.Values.ToHashSet();
        var stale = (await queries.Running(cancellationToken))
            .Where(run => !active.Contains(run.Id.Key))
            .ToList();

        foreach (var run in stale)
        {
            await commands.Update(
                run.Id.Key,
                run.Id.Version,
                run.ToDraft() with
                {
                    FinishedAt = clock.GetUtcNow(),
                    Status = ScheduleRunStatus.Failed,
                    Summary = InterruptedSummary
                },
                cancellationToken);
        }

        return stale.Count;
    }

    public async Task Stop()
    {
        lock (_gate)
        {
            _closed = true;
        }

        await _stopping.CancelAsync();
        await Task.WhenAll(_admissions.Values.ToList());
        await WhenIdle();
    }

    public void Dispose()
    {
        _stopping.Dispose();
        _slots.Dispose();
    }

    private async Task<Guid> Record(
        IScheduleQueries.ScheduleDetails schedule,
        DateTimeOffset scheduledFor,
        CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IScheduleRunCommands>().Create(
            new IScheduleRunCommands.ScheduleRunDraft(
                schedule.Id.Key,
                schedule.Name,
                schedule.Action,
                scheduledFor,
                clock.GetUtcNow(),
                null,
                ScheduleRunStatus.Running,
                string.Empty,
                null,
                ScheduleRunEmail.NotRequested,
                0),
            cancellationToken);

        return result is VersionedSaveResult.Saved saved
            ? saved.Id.Key
            : throw new InvalidOperationException($"Schedule run could not be recorded: {result}");
    }

    private async Task Execute(IScheduleQueries.ScheduleDetails schedule, Guid runId, DateTimeOffset scheduledFor)
    {
        var acquired = false;

        try
        {
            await _slots.WaitAsync(_stopping.Token);
            acquired = true;

            await using var scope = scopes.CreateAsyncScope();
            var outcome = await Invoke(scope.ServiceProvider, schedule, scheduledFor);
            var email = await TryNotify(scope.ServiceProvider, schedule, outcome);

            await Finish(scope.ServiceProvider, runId, outcome, outcome.Link.Map(link => link.ToHashRoute()), email);
        }
        catch (Exception exception)
        {
            var interrupted = _stopping.IsCancellationRequested;

            if (!interrupted)
            {
                logger.LogError(exception, "Scheduled run {RunId} of schedule {ScheduleId} failed", () => [runId, schedule.Id.Key]);
            }

            await TryFinishFailed(runId, interrupted ? InterruptedSummary : FailedSummaryPrefix + exception.Message);
        }
        finally
        {
            if (acquired)
            {
                _slots.Release();
            }

            _running.TryRemove(schedule.Id.Key, out _);
        }
    }

    private async Task<ScheduledActionResult> Invoke(
        IServiceProvider services,
        IScheduleQueries.ScheduleDetails schedule,
        DateTimeOffset scheduledFor) =>
        await services.GetServices<IScheduledAction>().FindAction(schedule.Action).Match(
            () => Task.FromResult(new ScheduledActionResult(false, UnknownActionSummary, None)),
            action => RunWithTimeout(action, schedule, scheduledFor));

    private async Task<ScheduledActionResult> RunWithTimeout(
        IScheduledAction action,
        IScheduleQueries.ScheduleDetails schedule,
        DateTimeOffset scheduledFor)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
        options.CurrentValue.RunTimeout.ForEach(limit => timeout.CancelAfter(limit));

        try
        {
            return await action.Run(
                new ScheduledActionInvocation(schedule.Id.Key, schedule.Name, scheduledFor, schedule.Parameters.Values),
                timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !_stopping.IsCancellationRequested)
        {
            return new ScheduledActionResult(false, TimedOutSummary, None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Scheduled action {Action} failed", () => [schedule.Action]);

            return new ScheduledActionResult(false, FailedSummaryPrefix + exception.Message, None);
        }
    }

    private async Task<(ScheduleRunEmail Status, int Recipients)> TryNotify(
        IServiceProvider services,
        IScheduleQueries.ScheduleDetails schedule,
        ScheduledActionResult outcome)
    {
        try
        {
            return await Notify(services, schedule, outcome);
        }
        catch (Exception exception)
        {
            if (!_stopping.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Schedule {ScheduleId} notification could not be prepared", () => [schedule.Id.Key]);
            }

            return (ScheduleRunEmail.Failed, 0);
        }
    }

    private async Task<(ScheduleRunEmail Status, int Recipients)> Notify(
        IServiceProvider services,
        IScheduleQueries.ScheduleDetails schedule,
        ScheduledActionResult outcome)
    {
        if (!schedule.Notify.Wants(outcome.Succeeded))
        {
            return (ScheduleRunEmail.NotRequested, 0);
        }

        var recipients = await services.GetRequiredService<IScheduleParticipantQueries>()
            .Emails(schedule.Participants, _stopping.Token);

        if (recipients.Count == 0)
        {
            return (ScheduleRunEmail.NoRecipients, 0);
        }

        var sender = services.GetRequiredService<IModuleEmailSender>();
        var frontend = services.GetRequiredService<IConfiguration>().FrontendUri();
        var absolute = outcome.Link.Bind(link => frontend.Map(uri => uri.ToFrontendLink(link.Route, link.Query)));
        var subject = schedule.Name.ToScheduleEmailSubject(outcome.Succeeded);
        var body = schedule.Name.ToScheduleEmailBody(outcome, absolute);
        var failures = 0;

        foreach (var recipient in recipients)
        {
            try
            {
                await sender.SendEmailAsync(recipient, subject, body, _stopping.Token);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures++;
                logger.LogWarning(exception, "Schedule {ScheduleId} email could not be sent", () => [schedule.Id.Key]);
            }
        }

        return (failures == 0 ? ScheduleRunEmail.Sent : ScheduleRunEmail.Failed, recipients.Count - failures);
    }

    private async Task Finish(
        IServiceProvider services,
        Guid runId,
        ScheduledActionResult outcome,
        Option<string> link,
        (ScheduleRunEmail Status, int Recipients) email)
    {
        var started = await services.GetRequiredService<IScheduleRunQueries>()
            .Find(runId, DeletionState.Active, CancellationToken.None);

        await started.Match(
            () => Task.CompletedTask,
            async run => await services.GetRequiredService<IScheduleRunCommands>().Update(
                runId,
                run.ToDraft() with
                {
                    FinishedAt = clock.GetUtcNow(),
                    Status = outcome.Succeeded ? ScheduleRunStatus.Succeeded : ScheduleRunStatus.Failed,
                    Summary = outcome.Summary,
                    Link = link.Where(value => value.Length <= ScheduleRun.MaxLinkLength)
                        .Match(() => (string?)null, value => value),
                    Email = email.Status,
                    Recipients = email.Recipients
                },
                CancellationToken.None));
    }

    private async Task TryFinishFailed(Guid runId, string summary)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await Finish(
                scope.ServiceProvider,
                runId,
                new ScheduledActionResult(false, summary, None),
                None,
                (ScheduleRunEmail.NotRequested, 0));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Scheduled run {RunId} could not be finalized", () => [runId]);
        }
    }
}
