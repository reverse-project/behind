using System.Security.Claims;
using Behind.Common;
using Behind.Common.Scheduling;
using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Domain;
using Behind.Core.WebApi.Scheduling;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public static class ScheduleDraftExtensions
{
    public const string InvalidCronReason = "invalid-cron";
    public const string UnknownActionReason = "unknown-action";

    extension(ScheduleEndpoints.SaveScheduleRequest request)
    {
        public async Task<Validation<IScheduleCommands.ScheduleDraft>> ToDraft(
            ClaimsPrincipal user,
            IEnumerable<IScheduledAction> actions,
            CancellationToken cancellationToken)
        {
            var cron = CronExpression.TryParse(request.Cron);
            var action = actions.FindAction(request.Action.Trim());
            var notify = string.IsNullOrWhiteSpace(request.Notify)
                ? Some(ScheduleNotification.Never)
                : request.Notify.ToScheduleNotification();
            var participants = request.ToParticipants();

            if (request.Name.Trim().Length is 0 or > Schedule.MaxNameLength || notify.IsNone())
            {
                return Invalid<IScheduleCommands.ScheduleDraft>(Error(ScheduleCommands.InvalidReason));
            }

            if (participants.IsNone())
            {
                return Invalid<IScheduleCommands.ScheduleDraft>(Error(ScheduleCommands.InvalidParticipantsReason));
            }

            return await cron.Match(
                () => Task.FromResult(Invalid<IScheduleCommands.ScheduleDraft>(Error(InvalidCronReason))),
                expression => request.ToDraft(user, expression, action, notify, participants, cancellationToken));
        }

        private async Task<Validation<IScheduleCommands.ScheduleDraft>> ToDraft(
            ClaimsPrincipal user,
            CronExpression cron,
            Option<IScheduledAction> action,
            Option<ScheduleNotification> notify,
            Option<ScheduleParticipants> participants,
            CancellationToken cancellationToken)
        {
            return await action.Match(
                () => Task.FromResult(Invalid<IScheduleCommands.ScheduleDraft>(Error(UnknownActionReason))),
                async found => await request.ToParameters(found).Match(
                    () => Task.FromResult(Invalid<IScheduleCommands.ScheduleDraft>(Error(ScheduleCommands.InvalidReason))),
                    async parameters => (await found.Validate(user, parameters.Values, cancellationToken)).Match(
                        () => Valid(new IScheduleCommands.ScheduleDraft(
                            request.Name.Trim(),
                            cron,
                            found.Descriptor.Key,
                            parameters,
                            request.Enabled,
                            notify.GetOrElse(ScheduleNotification.Never),
                            participants.GetOrElse(ScheduleParticipants.Empty))),
                        reason => Invalid<IScheduleCommands.ScheduleDraft>(Error(reason)))));
        }

        private Option<ScheduleParticipants> ToParticipants()
        {
            var items = (request.Participants ?? [])
                .Select(participant => participant.Kind.ToScheduleParticipantKind()
                    .Map(kind => new ScheduleParticipant(kind, (participant.Value ?? string.Empty).Trim())))
                .ToList();

            if (items.Any(item => item.IsNone()))
            {
                return None;
            }

            var normalized = new ScheduleParticipants(items.Bind(item => item).ToList()).Normalized;

            return normalized.IsValid ? Some(normalized) : None;
        }

        private Option<ScheduleParameters> ToParameters(IScheduledAction action)
        {
            var supplied = request.Parameters ?? new Dictionary<string, string?>();
            var values = action.Descriptor.Parameters
                .Select(parameter => (parameter, value: supplied.GetValueOrDefault(parameter.Name)?.Trim() ?? string.Empty))
                .ToList();

            return values.Any(entry => entry.parameter.Required && entry.value.Length == 0)
                ? None
                : Some(new ScheduleParameters(values
                    .Where(entry => entry.value.Length > 0)
                    .ToDictionary(entry => entry.parameter.Name, entry => entry.value, StringComparer.Ordinal)));
        }
    }
}
