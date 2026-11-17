using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.Database;
using Behind.Common.Scheduling;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Domain;
using Behind.Core.WebApi.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed class ScheduleEndpoints
{
    public const int DefaultPageSize = ManagedListExtensions.DefaultPageSize;
    public const int MaxPageSize = ManagedListExtensions.MaxPageSize;
    public const int MaxParticipants = 500;
    public const string AlreadyRunningReason = "already-running";

    public sealed record ScheduleParticipantRequest(
        [property: Required, MaxLength(Schedule.MaxNotifyLength)] string Kind,
        [property: Required, MaxLength(ScheduleParticipant.MaxValueLength)] string? Value);

    public sealed record SaveScheduleRequest(
        [property: Required, MaxLength(Schedule.MaxNameLength * 2)] string Name,
        [property: Required, MaxLength(Schedule.MaxCronLength)] string Cron,
        [property: Required, MaxLength(Schedule.MaxActionLength)] string Action,
        Dictionary<string, string?>? Parameters,
        bool Enabled,
        [property: MaxLength(Schedule.MaxNotifyLength)] string? Notify,
        [property: MaxLength(MaxParticipants)] IReadOnlyList<ScheduleParticipantRequest>? Participants,
        long? Version = null);

    [IgnoreETag]
    public static Task<IResult> Manage(
        HttpContext httpContext,
        IScheduleQueries queries,
        IAuthorizationService authorization,
        TimeProvider clock,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? search = null,
        [FromQuery] string? letter = null,
        [FromQuery] bool deleted = false) =>
        httpContext.WithManagedList(
            authorization,
            search,
            letter,
            deleted,
            async query =>
            {
                var now = clock.GetUtcNow();
                var schedules = await queries.List(
                    Flipper.Create(page, pageSize, DefaultPageSize, MaxPageSize),
                    query.Search,
                    query.Letter,
                    query.State,
                    httpContext.RequestAborted);

                var letters = await queries.GetLetters(query.Search, query.State, httpContext.RequestAborted);

                return Results.Json(new ScheduleManageResponse(
                    schedules.ToPageResponse(schedule => schedule.ToItem(query.State, now)),
                    letters));
            });

    [IgnoreETag]
    public static async Task<IResult> Get(
        Guid id,
        HttpContext httpContext,
        IScheduleQueries queries,
        IScheduleParticipantQueries participants,
        IAuthorizationService authorization,
        TimeProvider clock,
        IOptionsMonitor<SchedulerConfiguration> options)
    {
        httpContext.Response.Headers.CacheControl = ManagedListExtensions.NoStore;

        var active = await queries.Find(id, DeletionState.Active, httpContext.RequestAborted);
        var state = active.Match(() => DeletionState.Deleted, _ => DeletionState.Active);
        var found = await active.Match(
            async () => await authorization.HoldsGrantAsync(httpContext.User, ManagedListExtensions.DeletedRecordsGrant)
                ? await queries.Find(id, DeletionState.Deleted, httpContext.RequestAborted)
                : None,
            schedule => Task.FromResult(Some(schedule)));

        return await found.Match(
            () => Task.FromResult(Results.NotFound()),
            async schedule => Results.Json(schedule.ToResponse(
                state,
                await participants.Label(schedule.Participants, httpContext.RequestAborted),
                clock.GetUtcNow(),
                options.CurrentValue.DefaultPreview)));
    }

    [IgnoreETag]
    public static async Task<IResult> Create(
        SaveScheduleRequest request,
        HttpContext httpContext,
        IScheduleCommands commands,
        IEnumerable<IScheduledAction> actions) =>
        await (await request.ToDraft(httpContext.User, actions, httpContext.RequestAborted)).Match(
            errors => Task.FromResult(Rejected(errors)),
            async draft => (await commands.Create(draft, httpContext.RequestAborted)).ToResult());

    [IgnoreETag]
    public static async Task<IResult> Update(
        Guid id,
        SaveScheduleRequest request,
        HttpContext httpContext,
        IScheduleCommands commands,
        IScheduleQueries queries,
        IEnumerable<IScheduledAction> actions)
    {
        var existing = await queries.Find(id, DeletionState.Active, httpContext.RequestAborted);

        if (existing.IsNone())
        {
            return Results.NotFound();
        }

        return await (await request.ToDraft(httpContext.User, actions, httpContext.RequestAborted)).Match(
            errors => Task.FromResult(Rejected(errors)),
            async draft => (await (request.Version is { } version
                ? commands.Update(id, version, draft, httpContext.RequestAborted)
                : commands.Update(id, draft, httpContext.RequestAborted))).ToResult());
    }

    [IgnoreETag]
    public static async Task<IResult> Delete(Guid id, HttpContext httpContext, IScheduleCommands commands) =>
        (await commands.Delete(id, httpContext.RequestAborted)).ToNoContentResult();

    [IgnoreETag]
    public static async Task<IResult> Restore(Guid id, HttpContext httpContext, IScheduleCommands commands) =>
        (await commands.Restore(id, httpContext.RequestAborted)).ToNoContentResult();

    [IgnoreETag]
    public static async Task<IResult> HardDelete(Guid id, HttpContext httpContext, IScheduleCommands commands) =>
        (await commands.HardDelete(id, httpContext.RequestAborted)).ToResult();

    [IgnoreETag]
    public static async Task<IResult> Run(
        Guid id,
        HttpContext httpContext,
        IScheduleQueries queries,
        IScheduleRunner runner,
        TimeProvider clock)
    {
        var schedule = await queries.Find(id, DeletionState.Active, httpContext.RequestAborted);

        return await schedule.Match(
            () => Task.FromResult(Results.NotFound()),
            async found => (await runner.TryStart(found, clock.GetUtcNow().ToScheduleMinute(), httpContext.RequestAborted))
                .Match(
                    () => Results.Conflict(new VersionedConflictResponse(AlreadyRunningReason)),
                    runId => Results.Accepted(value: new ScheduleRunStartedResponse(runId))));
    }

    [IgnoreETag]
    public static IResult Actions(HttpContext httpContext, IEnumerable<IScheduledAction> actions)
    {
        httpContext.Response.Headers.CacheControl = ManagedListExtensions.NoStore;

        return Results.Json(actions
            .OrderBy(action => action.Descriptor.Key, StringComparer.Ordinal)
            .Select(action => action.ToResponse())
            .ToList());
    }

    [IgnoreETag]
    public static async Task<IResult> Options(
        string key,
        string parameter,
        HttpContext httpContext,
        IEnumerable<IScheduledAction> actions)
    {
        httpContext.Response.Headers.CacheControl = ManagedListExtensions.NoStore;

        var action = actions.FindAction(key)
            .Where(found => found.Descriptor.Parameters.Any(item => item.Name == parameter));

        return await action.Match(
            () => Task.FromResult(Results.NotFound()),
            async found => Results.Json(await found.Options(httpContext.User, parameter, httpContext.RequestAborted)));
    }

    [IgnoreETag]
    public static IResult Cron(
        HttpContext httpContext,
        TimeProvider clock,
        IOptionsMonitor<SchedulerConfiguration> options,
        [FromQuery] string? expression = null,
        [FromQuery] int? count = null)
    {
        httpContext.Response.Headers.CacheControl = ManagedListExtensions.NoStore;

        var configuration = options.CurrentValue;
        var limit = Math.Clamp(count ?? configuration.DefaultPreview, 1, configuration.PreviewLimit);

        return expression is { Length: > Schedule.MaxCronLength }
            ? Results.BadRequest()
            : Results.Json(CronExpression.Parse(expression).ToResponse(clock.GetUtcNow(), limit));
    }

    [IgnoreETag]
    public static async Task<IResult> Participants(
        HttpContext httpContext,
        IScheduleParticipantQueries participants,
        IOptionsMonitor<SchedulerConfiguration> options,
        [FromQuery] string? search = null)
    {
        httpContext.Response.Headers.CacheControl = ManagedListExtensions.NoStore;

        var term = search.ToSearchTerm(ManagedListExtensions.MaxSearchLength).GetOrElse(string.Empty);
        var directory = await participants.Directory(term, options.CurrentValue.ParticipantLimit, httpContext.RequestAborted);

        return Results.Json(directory.ToResponse());
    }

    [IgnoreETag]
    public static async Task<IResult> Runs(
        HttpContext httpContext,
        IScheduleRunQueries runs,
        IAuthorizationService authorization,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] Guid? scheduleId = null,
        [FromQuery] string? status = null,
        [FromQuery] bool deleted = false)
    {
        var statusFilter = status.ToScheduleRunStatus();

        if (!string.IsNullOrWhiteSpace(status) && statusFilter.IsNone())
        {
            return Results.BadRequest();
        }

        return await httpContext.WithDeletionState(
            authorization,
            deleted,
            async state =>
            {
                var result = await runs.List(
                    Flipper.Create(page, pageSize, DefaultPageSize, MaxPageSize),
                    scheduleId is { } key ? key : None,
                    statusFilter,
                    state,
                    httpContext.RequestAborted);

                return Results.Json(result.ToPageResponse(run => run.ToResponse(state)));
            });
    }

    [IgnoreETag]
    public static async Task<IResult> DeleteRun(Guid id, HttpContext httpContext, IScheduleRunCommands commands) =>
        (await commands.Delete(id, httpContext.RequestAborted)).ToNoContentResult();

    [IgnoreETag]
    public static async Task<IResult> RestoreRun(Guid id, HttpContext httpContext, IScheduleRunCommands commands) =>
        (await commands.Restore(id, httpContext.RequestAborted)).ToNoContentResult();

    [IgnoreETag]
    public static async Task<IResult> HardDeleteRun(Guid id, HttpContext httpContext, IScheduleRunCommands commands) =>
        (await commands.HardDelete(id, httpContext.RequestAborted)).ToResult();

    private static IResult Rejected(IEnumerable<Error> errors) =>
        Results.Conflict(new VersionedConflictResponse(
            errors.Select(error => error.Message).OfType<string>().DefaultIfEmpty(ScheduleCommands.InvalidReason).First()));
}
