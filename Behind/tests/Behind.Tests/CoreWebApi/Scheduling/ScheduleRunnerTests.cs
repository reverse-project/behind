using Behind.Common.WebApi;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.WebApi.Scheduling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static Behind.Tests.Helpers.ScheduleTestBedExtensions;

namespace Behind.Tests.CoreWebApi.Scheduling;

public sealed class ScheduleRunnerTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 30, TimeSpan.Zero);

    private readonly TestDbContextFactory _factory = new();
    private readonly AdjustableTimeProvider _clock = new(Now);
    private readonly IModuleEmailSender _sender = Substitute.For<IModuleEmailSender>();
    private readonly List<IScheduledAction> _actions = [];
    private SchedulerConfiguration _configuration = new();
    private ServiceProvider? _services;

    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        _factory.Dispose();
    }

    private ServiceProvider Services()
    {
        if (_services is not null)
        {
            return _services;
        }

        using (var seed = _factory.CreateContext())
        {
            DatabaseSeedHelper.EnsureBuiltInUsersAsync(seed).GetAwaiter().GetResult();
        }

        var options = Substitute.For<IOptionsMonitor<SchedulerConfiguration>>();
        options.CurrentValue.Returns(_ => _configuration);
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(_clock);
        services.AddSingleton(options);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:Url"] = "https://example.test/app/" })
            .Build());
        services.AddLogging();
        services.AddScoped<AbstractCoreContext>(_ => _factory.CreateAuditedContext(AuditActor.Autonomous, _clock));
        services.AddScoped<IScheduleQueries, ScheduleQueries>();
        services.AddScoped<IScheduleCommands, ScheduleCommands>();
        services.AddScoped<IScheduleRunQueries, ScheduleRunQueries>();
        services.AddScoped<IScheduleRunCommands, ScheduleRunCommands>();
        services.AddScoped<IScheduleParticipantQueries, ScheduleParticipantQueries>();
        services.AddSingleton(_sender);
        services.AddSingleton<IScheduleRunner, ScheduleRunner>();
        services.AddSingleton<ScheduleService>();

        foreach (var action in _actions)
        {
            services.AddSingleton(action);
        }

        return _services = services.BuildServiceProvider();
    }

    private IScheduleRunner Runner => Services().GetRequiredService<IScheduleRunner>();

    private async Task<IScheduleQueries.ScheduleDetails> ScheduleAsync(IScheduleCommands.ScheduleDraft? draft = null)
    {
        await using var scope = Services().CreateAsyncScope();
        var saved = await scope.ServiceProvider.GetRequiredService<IScheduleCommands>().Create(draft ?? ScheduleDraft());
        var key = Assert.IsType<VersionedSaveResult.Saved>(saved).Id.Key;

        return (await scope.ServiceProvider.GetRequiredService<IScheduleQueries>().Find(key, DeletionState.Active))
            .Match(() => throw new InvalidOperationException(), schedule => schedule);
    }

    private async Task<IScheduleRunQueries.ScheduleRunDetails> RunAsync(Guid runId)
    {
        await using var scope = Services().CreateAsyncScope();

        return (await scope.ServiceProvider.GetRequiredService<IScheduleRunQueries>().Find(runId, DeletionState.Active))
            .Match(() => throw new InvalidOperationException(), run => run);
    }

    private async Task<IReadOnlyList<IScheduleRunQueries.ScheduleRunDetails>> RunsAsync()
    {
        await using var scope = Services().CreateAsyncScope();
        var page = await scope.ServiceProvider.GetRequiredService<IScheduleRunQueries>()
            .List(new Flipper(1, 100), None, None, DeletionState.Active);

        return page.Results.Match(() => [], items => items.ToList());
    }

    private async Task<IScheduleRunQueries.ScheduleRunDetails> StartAndWaitAsync(IScheduleQueries.ScheduleDetails schedule)
    {
        var runId = (await Runner.TryStart(schedule, Now.ToScheduleMinute()))
            .Match(() => throw new InvalidOperationException("not started"), id => id);
        await Runner.WhenIdle();

        return await RunAsync(runId);
    }

    [Fact]
    public async Task SuccessfulRun_RecordsTheLinkAndEmailsTheParticipants()
    {
        var action = new FakeScheduledAction();
        _actions.Add(action);
        var schedule = await ScheduleAsync(ScheduleDraft(
            "Nightly <audit>",
            parameters: new Dictionary<string, string> { [FakeScheduledAction.ReportParameter] = "r1" },
            notify: ScheduleNotification.Always,
            participants: [new(ScheduleParticipantKind.Email, "ops@example.test")]));

        var run = await StartAndWaitAsync(schedule);

        Assert.Equal(ScheduleRunStatus.Succeeded, run.Status);
        Assert.Equal("Report created", run.Summary);
        Assert.Equal("#/reports/42?tab=summary", run.Link);
        Assert.Equal((ScheduleRunEmail.Sent, 1), (run.Email, run.Recipients));
        Assert.Equal(Now.ToScheduleMinute(), run.ScheduledFor);
        Assert.NotNull(run.FinishedAt);
        var invocation = Assert.Single(action.Invocations);
        Assert.Equal((schedule.Id.Key, "Nightly <audit>", "r1"), (invocation.ScheduleId, invocation.ScheduleName, invocation.Parameters[FakeScheduledAction.ReportParameter]));
        await _sender.Received(1).SendEmailAsync(
            "ops@example.test",
            "Nightly <audit>: succeeded",
            Arg.Is<string>(body => body.Contains("https://example.test/app/#/reports/42?tab=summary") && body.Contains("Nightly &lt;audit&gt;")),
            Arg.Any<CancellationToken>());
        Assert.False(Runner.IsRunning(schedule.Id.Key));
    }

    [Fact]
    public async Task UnknownAction_FailsTheRunWithoutCallingAnything()
    {
        var schedule = await ScheduleAsync(ScheduleDraft(action: "missing.action", notify: ScheduleNotification.Success,
            participants: [new(ScheduleParticipantKind.Email, "ops@example.test")]));

        var run = await StartAndWaitAsync(schedule);

        Assert.Equal((ScheduleRunStatus.Failed, ScheduleRunner.UnknownActionSummary), (run.Status, run.Summary));
        Assert.Equal(ScheduleRunEmail.NotRequested, run.Email);
        await _sender.DidNotReceiveWithAnyArgs().SendEmailAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task ThrowingAction_FailsTheRunAndNotifiesOnFailure()
    {
        _actions.Add(new FakeScheduledAction((_, _) => throw new InvalidOperationException("boom")));
        var schedule = await ScheduleAsync(ScheduleDraft(notify: ScheduleNotification.Failure,
            participants: [new(ScheduleParticipantKind.Email, "ops@example.test")]));

        var run = await StartAndWaitAsync(schedule);

        Assert.Equal((ScheduleRunStatus.Failed, ScheduleRunner.FailedSummaryPrefix + "boom"), (run.Status, run.Summary));
        Assert.Null(run.Link);
        Assert.Equal(ScheduleRunEmail.Sent, run.Email);
        await _sender.Received(1).SendEmailAsync("ops@example.test", Arg.Is<string>(subject => subject.EndsWith("failed")),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyWithoutResolvableRecipients_RecordsNoRecipients()
    {
        _actions.Add(new FakeScheduledAction());
        var schedule = await ScheduleAsync(ScheduleDraft(notify: ScheduleNotification.Always,
            participants: [new(ScheduleParticipantKind.Role, Guid.NewGuid().ToString())]));

        var run = await StartAndWaitAsync(schedule);

        Assert.Equal((ScheduleRunEmail.NoRecipients, 0), (run.Email, run.Recipients));
    }

    [Fact]
    public async Task FailingEmail_IsRecordedWithoutFailingTheRun()
    {
        _actions.Add(new FakeScheduledAction());
        _sender.SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("smtp down")));
        var schedule = await ScheduleAsync(ScheduleDraft(notify: ScheduleNotification.Success,
            participants: [new(ScheduleParticipantKind.Email, "ops@example.test")]));

        var run = await StartAndWaitAsync(schedule);

        Assert.Equal(ScheduleRunStatus.Succeeded, run.Status);
        Assert.Equal((ScheduleRunEmail.Failed, 0), (run.Email, run.Recipients));
    }

    [Fact]
    public async Task TryStart_RefusesASecondRunOfARunningSchedule()
    {
        var release = new TaskCompletionSource();
        _actions.Add(new FakeScheduledAction(async (_, token) =>
        {
            await release.Task.WaitAsync(token);

            return new ScheduledActionResult(true, "ok", None);
        }));
        var schedule = await ScheduleAsync();

        var first = await Runner.TryStart(schedule, Now.ToScheduleMinute());
        var second = await Runner.TryStart(schedule, Now.ToScheduleMinute());

        Assert.False(first.IsNone());
        Assert.True(second.IsNone());
        Assert.True(Runner.IsRunning(schedule.Id.Key));
        Assert.Equal(ScheduleRunStatus.Running, Assert.Single(await RunsAsync()).Status);

        release.SetResult();
        await Runner.WhenIdle();

        Assert.False(Runner.IsRunning(schedule.Id.Key));
        Assert.Equal(ScheduleRunStatus.Succeeded, Assert.Single(await RunsAsync()).Status);
    }

    [Fact]
    public async Task Stop_InterruptsRunningActionsAndRejectsNewRuns()
    {
        _actions.Add(new FakeScheduledAction(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);

            return new ScheduledActionResult(true, "never", None);
        }));
        var schedule = await ScheduleAsync();
        await Runner.TryStart(schedule, Now.ToScheduleMinute());

        await Runner.Stop();

        var run = Assert.Single(await RunsAsync());
        Assert.Equal((ScheduleRunStatus.Failed, ScheduleRunner.InterruptedSummary), (run.Status, run.Summary));
        Assert.True((await Runner.TryStart(schedule, Now.ToScheduleMinute())).IsNone());
    }

    [Fact]
    public async Task RecoverInterrupted_FailsRunsLeftRunningByAPreviousProcess()
    {
        var schedule = await ScheduleAsync();
        await using (var scope = Services().CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IScheduleRunCommands>()
                .Create(ScheduleRunDraft(schedule.Id.Key, Now.AddHours(-1), status: ScheduleRunStatus.Running));
        }

        Assert.Equal(1, await Runner.RecoverInterrupted());

        var run = Assert.Single(await RunsAsync());
        Assert.Equal((ScheduleRunStatus.Failed, ScheduleRunner.InterruptedSummary), (run.Status, run.Summary));
        Assert.Equal(Now, run.FinishedAt);
    }

    [Fact]
    public async Task Tick_StartsDueSchedulesOncePerMinuteAndSkipsDisabledOnes()
    {
        var action = new FakeScheduledAction();
        _actions.Add(action);
        var every = await ScheduleAsync(ScheduleDraft("Every minute", "* * * * *"));
        await ScheduleAsync(ScheduleDraft("Disabled", "* * * * *", enabled: false));
        await ScheduleAsync(ScheduleDraft("Later", "0 3 * * *"));
        var service = Services().GetRequiredService<ScheduleService>();

        Assert.Equal(1, await service.TickAsync());
        await Runner.WhenIdle();
        Assert.Equal(0, await service.TickAsync());

        _clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(1, await service.TickAsync());
        await Runner.WhenIdle();

        Assert.Equal(
            [Now.ToScheduleMinute(), Now.ToScheduleMinute().AddMinutes(1)],
            action.Invocations.Select(invocation => invocation.ScheduledFor).Order());
        Assert.All(action.Invocations, invocation => Assert.Equal(every.Id.Key, invocation.ScheduleId));
    }

    [Fact]
    public async Task Tick_CatchesUpOnlyTheConfiguredWindowAndRunsTheLatestDueMinute()
    {
        _configuration = new SchedulerConfiguration { MaxCatchUpMinutes = 5 };
        var action = new FakeScheduledAction();
        _actions.Add(action);
        await ScheduleAsync(ScheduleDraft("Quarter", "*/15 * * * *"));
        await ScheduleAsync(ScheduleDraft("Ten past", "10 * * * *"));
        var service = Services().GetRequiredService<ScheduleService>();
        await service.TickAsync();
        await Runner.WhenIdle();
        action.Invocations.Clear();

        _clock.Advance(TimeSpan.FromMinutes(17));
        Assert.Equal(1, await service.TickAsync());
        await Runner.WhenIdle();

        var invocation = Assert.Single(action.Invocations);
        Assert.Equal((Now.ToScheduleMinute().AddMinutes(15), "Quarter"), (invocation.ScheduledFor, invocation.ScheduleName));
    }

    [Fact]
    public async Task Tick_SkipsSchedulesThatAreStillRunning()
    {
        var release = new TaskCompletionSource();
        _actions.Add(new FakeScheduledAction(async (_, token) =>
        {
            await release.Task.WaitAsync(token);

            return new ScheduledActionResult(true, "ok", None);
        }));
        await ScheduleAsync(ScheduleDraft("Every minute", "* * * * *"));
        var service = Services().GetRequiredService<ScheduleService>();

        Assert.Equal(1, await service.TickAsync());
        _clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(0, await service.TickAsync());

        release.SetResult();
        await Runner.WhenIdle();
        Assert.Single(await RunsAsync());
    }
}
