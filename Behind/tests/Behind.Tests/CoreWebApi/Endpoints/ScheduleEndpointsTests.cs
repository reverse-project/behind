using System.Security.Claims;
using Behind.Common.WebApi;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using static Behind.Tests.Helpers.ScheduleTestBedExtensions;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class ScheduleEndpointsTests
{
    private static readonly Guid Id = Guid.Parse("18fdcc69-269d-4c1a-9123-56f83cd36d50");
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 30, TimeSpan.Zero);

    private readonly IScheduleCommands _commands = Substitute.For<IScheduleCommands>();
    private readonly FakeScheduledAction _action = new();

    public ScheduleEndpointsTests()
    {
        _commands.Create(default!, default).ReturnsForAnyArgs(new VersionedSaveResult.Saved(new VersionedKey<Guid>(Id, 1)));
    }

    private static ScheduleEndpoints.SaveScheduleRequest Request(
        string name = " Nightly ",
        string cron = "0 2 * * *",
        string action = TestAction,
        Dictionary<string, string?>? parameters = null,
        string? notify = "success",
        IReadOnlyList<ScheduleEndpoints.ScheduleParticipantRequest>? participants = null) =>
        new(
            name,
            cron,
            action,
            parameters ?? new Dictionary<string, string?> { [FakeScheduledAction.ReportParameter] = " r1 " },
            true,
            notify,
            participants ?? [new("email", "ops@example.test"), new("role", "11111111-1111-1111-1111-111111111111")]);

    private static IOptionsMonitor<SchedulerConfiguration> Options(SchedulerConfiguration? configuration = null)
    {
        var options = Substitute.For<IOptionsMonitor<SchedulerConfiguration>>();
        options.CurrentValue.Returns(configuration ?? new SchedulerConfiguration());

        return options;
    }

    private static IScheduleQueries.ScheduleDetails Details() =>
        new(new VersionedKey<Guid>(Id, 3), "Nightly", Cron("0 2 * * *"), TestAction, ScheduleParameters.Empty, true,
            ScheduleNotification.Never, ScheduleParticipants.Empty);

    private static string ConflictReason(IResult result) =>
        Assert.IsType<VersionedConflictResponse>(Assert.IsAssignableFrom<IValueHttpResult>(result).Value).Reason;

    private Task<IResult> CreateAsync(ScheduleEndpoints.SaveScheduleRequest request) =>
        ScheduleEndpoints.Create(request, new DefaultHttpContext(), _commands, [_action]);

    [Fact]
    public async Task Create_ValidRequest_SavesTheNormalizedDraftWithKnownParametersOnly()
    {
        var result = await CreateAsync(Request(parameters: new Dictionary<string, string?>
        {
            [FakeScheduledAction.ReportParameter] = " r1 ",
            [FakeScheduledAction.NoteParameter] = "  ",
            ["unknown"] = "dropped"
        }));

        Assert.Equal(Id, Assert.IsType<JsonHttpResult<VersionedSavedResponse>>(result).Value!.Id);
        await _commands.Received(1).Create(
            Arg.Is<IScheduleCommands.ScheduleDraft>(draft =>
                draft.Name == "Nightly"
                && draft.Cron.Text == "0 2 * * *"
                && draft.Notify == ScheduleNotification.Success
                && draft.Parameters.Values.Count == 1
                && draft.Parameters.Values[FakeScheduledAction.ReportParameter] == "r1"
                && draft.Participants.Items.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(" ", "0 2 * * *", TestAction, "success", "invalid")]
    [InlineData("Name", "0 2 * *", TestAction, "success", ScheduleDraftExtensions.InvalidCronReason)]
    [InlineData("Name", "0 2 * * *", "missing", "success", ScheduleDraftExtensions.UnknownActionReason)]
    [InlineData("Name", "0 2 * * *", TestAction, "sometimes", "invalid")]
    public async Task Create_InvalidRequest_IsAConflictWithItsReason(string name, string cron, string action, string notify, string reason)
    {
        var result = await CreateAsync(Request(name, cron, action, notify: notify));

        Assert.Equal(reason, ConflictReason(result));
        await _commands.DidNotReceiveWithAnyArgs().Create(default!, default);
    }

    [Fact]
    public async Task Create_MissingRequiredParameter_IsInvalid()
    {
        var result = await CreateAsync(Request(parameters: new Dictionary<string, string?> { [FakeScheduledAction.NoteParameter] = "x" }));

        Assert.Equal(ScheduleCommands.InvalidReason, ConflictReason(result));
    }

    [Theory]
    [InlineData("robot", "x")]
    [InlineData("email", "not-an-email")]
    [InlineData("user", "nope")]
    public async Task Create_InvalidParticipant_IsRejected(string kind, string value)
    {
        var result = await CreateAsync(Request(participants: [new(kind, value)]));

        Assert.Equal(ScheduleCommands.InvalidParticipantsReason, ConflictReason(result));
    }

    [Fact]
    public async Task Create_ActionValidationReason_IsReturned()
    {
        _action.Rejection = Some("auditor-graph-not-found");

        Assert.Equal("auditor-graph-not-found", ConflictReason(await CreateAsync(Request())));
    }

    [Fact]
    public async Task Create_WithoutNotifyOrParticipants_DefaultsToNever()
    {
        await CreateAsync(Request(notify: null, participants: []));

        await _commands.Received(1).Create(
            Arg.Is<IScheduleCommands.ScheduleDraft>(draft => draft.Notify == ScheduleNotification.Never && draft.Participants.Items.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_MissingSchedule_IsNotFound()
    {
        var queries = Substitute.For<IScheduleQueries>();
        queries.Find(default, default, default).ReturnsForAnyArgs(None);

        var result = await ScheduleEndpoints.Update(Id, Request(), new DefaultHttpContext(), _commands, queries, [_action]);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Run_StartsTheScheduleForTheCurrentMinute()
    {
        var queries = Substitute.For<IScheduleQueries>();
        queries.Find(Id, DeletionState.Active, Arg.Any<CancellationToken>()).Returns(Some(Details()));
        var runner = Substitute.For<IScheduleRunner>();
        var runId = Guid.NewGuid();
        runner.TryStart(default!, default, default).ReturnsForAnyArgs(Some(runId));

        var result = await ScheduleEndpoints.Run(Id, new DefaultHttpContext(), queries, runner, new FixedTimeProvider(Now));

        Assert.Equal(runId, Assert.IsType<Accepted<ScheduleRunStartedResponse>>(result).Value!.RunId);
        await runner.Received(1).TryStart(Arg.Any<IScheduleQueries.ScheduleDetails>(), Now.ToScheduleMinute(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_AlreadyRunning_IsAConflictAndMissingIsNotFound()
    {
        var queries = Substitute.For<IScheduleQueries>();
        queries.Find(Id, DeletionState.Active, Arg.Any<CancellationToken>()).Returns(Some(Details()));
        var runner = Substitute.For<IScheduleRunner>();
        runner.TryStart(default!, default, default).ReturnsForAnyArgs(Task.FromResult<Option<Guid>>(None));

        var busy = await ScheduleEndpoints.Run(Id, new DefaultHttpContext(), queries, runner, new FixedTimeProvider(Now));
        var missing = await ScheduleEndpoints.Run(Guid.NewGuid(), new DefaultHttpContext(), queries, runner, new FixedTimeProvider(Now));

        Assert.Equal(ScheduleEndpoints.AlreadyRunningReason, ConflictReason(busy));
        Assert.IsType<NotFound>(missing);
    }

    [Fact]
    public void Cron_DescribesValidExpressionsWithClampedPreview()
    {
        var http = new DefaultHttpContext();

        var result = ScheduleEndpoints.Cron(http, new FixedTimeProvider(Now), Options(new SchedulerConfiguration { MaxPreviewCount = 3 }), "*/30 * * * *", 10);

        var body = Assert.IsType<JsonHttpResult<ScheduleCronResponse>>(result).Value!;
        Assert.True(body.Valid);
        Assert.Equal("*/30", body.Fields!.Minute);
        Assert.Equal([Now.ToScheduleMinute().AddMinutes(30), Now.ToScheduleMinute().AddMinutes(60), Now.ToScheduleMinute().AddMinutes(90)], body.Next);
        Assert.Equal(ManagedListExtensions.NoStore, http.Response.Headers.CacheControl);
    }

    [Fact]
    public void Cron_InvalidExpression_IsDescribedAsInvalid()
    {
        var result = ScheduleEndpoints.Cron(new DefaultHttpContext(), new FixedTimeProvider(Now), Options(), "61 * * * *");

        var body = Assert.IsType<JsonHttpResult<ScheduleCronResponse>>(result).Value!;
        Assert.False(body.Valid);
        Assert.False(string.IsNullOrEmpty(body.Reason));
        Assert.Empty(body.Next);
    }

    [Fact]
    public void Actions_ListsTheDescriptors()
    {
        var result = ScheduleEndpoints.Actions(new DefaultHttpContext(), [_action]);

        var action = Assert.Single(Assert.IsType<JsonHttpResult<List<ScheduledActionResponse>>>(result).Value!);
        Assert.Equal(TestAction, action.Key);
        Assert.Equal(["choice", "template"], action.Parameters.Select(parameter => parameter.Kind));
    }

    [Fact]
    public async Task Options_UnknownParameter_IsNotFound()
    {
        Assert.IsType<NotFound>(await ScheduleEndpoints.Options(TestAction, "missing", new DefaultHttpContext(), [_action]));
        Assert.IsType<NotFound>(await ScheduleEndpoints.Options("missing", FakeScheduledAction.ReportParameter, new DefaultHttpContext(), [_action]));

        var result = await ScheduleEndpoints.Options(TestAction, FakeScheduledAction.ReportParameter, new DefaultHttpContext(), [_action]);
        Assert.Equal("r1", Assert.Single(Assert.IsType<JsonHttpResult<IReadOnlyList<ScheduledActionOption>>>(result).Value!).Value);
    }

    [Fact]
    public async Task Runs_UnknownStatus_IsABadRequest()
    {
        var result = await ScheduleEndpoints.Runs(
            new DefaultHttpContext(), Substitute.For<IScheduleRunQueries>(), Substitute.For<IAuthorizationService>(), status: "exploded");

        Assert.IsType<BadRequest>(result);
    }

    [Fact]
    public async Task Participants_UsesTheConfiguredUserLimit()
    {
        var participants = Substitute.For<IScheduleParticipantQueries>();
        participants.Directory(default, default, default).ReturnsForAnyArgs(new IScheduleParticipantQueries.ParticipantDirectory(
            [new(Guid.NewGuid(), "Auditors")], []));

        var result = await ScheduleEndpoints.Participants(
            new DefaultHttpContext(), participants, Options(new SchedulerConfiguration { ParticipantSearchLimit = 7 }), " aud ");

        Assert.Equal("Auditors", Assert.Single(Assert.IsType<JsonHttpResult<ScheduleParticipantDirectoryResponse>>(result).Value!.Roles).Name);
        await participants.Received(1).Directory("aud", 7, Arg.Any<CancellationToken>());
    }
}
