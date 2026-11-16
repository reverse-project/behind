using System.Globalization;
using System.Security.Claims;
using Behind.Common;
using Behind.Common.WebApi;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Microsoft.Extensions.Options;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Scheduling;

public sealed class SessionCleanupScheduledAction(
    ISessionCommands sessions,
    IOptionsMonitor<SchedulerConfiguration> configuration) : IScheduledAction
{
    public const string ActionKey = "core.session-cleanup";
    public const string AgeParameter = "invalidatedAgeDays";
    public const string InvalidAgeReason = "session-cleanup-invalid-age";

    public ScheduledActionDescriptor Descriptor =>
        new(ActionKey, "schedules.sessionCleanup.action",
            Array.AsReadOnly<ScheduledActionParameter>([
                new(AgeParameter, "schedules.sessionCleanup.invalidatedAgeDays", ScheduledActionParameterKind.Number, false)
                {
                    DefaultValue = SchedulerConfiguration.DefaultSessionCleanupAgeDays.ToString(CultureInfo.InvariantCulture),
                    Minimum = 1,
                    Maximum = configuration.CurrentValue.SessionCleanupAgeLimit
                }
            ]));

    public Task<IReadOnlyList<ScheduledActionOption>> Options(
        ClaimsPrincipal user, string parameter, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ScheduledActionOption>>([]);

    public Task<Option<string>> Validate(
        ClaimsPrincipal user, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken) =>
        Task.FromResult(AgeDays(parameters).Match(() => Some(InvalidAgeReason), _ => None));

    public async Task<ScheduledActionResult> Run(ScheduledActionInvocation invocation, CancellationToken cancellationToken) =>
        await AgeDays(invocation.Parameters).Match(
            () => Task.FromResult(new ScheduledActionResult(false, "The invalidated session age is invalid.", None)),
            async days =>
            {
                var removed = await sessions.DeleteInvalidatedBefore(invocation.ScheduledFor.AddDays(-days), cancellationToken);
                return new ScheduledActionResult(
                    true,
                    $"Removed {removed} invalidated session(s) older than {days} day(s).",
                    None);
            });

    private Option<int> AgeDays(IReadOnlyDictionary<string, string> parameters) =>
        !parameters.TryGetValue(AgeParameter, out var value) || string.IsNullOrWhiteSpace(value)
            ? Some(SchedulerConfiguration.DefaultSessionCleanupAgeDays)
            : int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var days)
              && days >= 1 && days <= configuration.CurrentValue.SessionCleanupAgeLimit
                ? Some(days)
                : None;
}
