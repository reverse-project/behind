using Behind.Common;
using Behind.Common.Database;
using Behind.Common.Scheduling;
using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.Domain;

namespace Behind.Core.WebApi.Endpoints;

public static class ScheduleResponseExtensions
{
    extension(IScheduleQueries.ScheduleListing schedule)
    {
        public ManagedSchedule ToItem(DeletionState state, DateTimeOffset now) =>
            new(
                schedule.Id.Key,
                schedule.Name,
                schedule.Cron.Text,
                schedule.Action,
                schedule.Enabled,
                schedule.Notify.Key,
                schedule.Enabled && state == DeletionState.Active
                    ? schedule.Cron.Next(now).Match(() => (DateTimeOffset?)null, next => next)
                    : null,
                state == DeletionState.Deleted);
    }

    extension(IScheduleQueries.ScheduleDetails schedule)
    {
        public ScheduleDetailsResponse ToResponse(
            DeletionState state,
            IReadOnlyList<IScheduleParticipantQueries.LabeledParticipant> participants,
            DateTimeOffset now,
            int previewCount) =>
            new(
                schedule.Id.Key,
                schedule.Id.Version,
                schedule.Name,
                schedule.Cron.Text,
                schedule.Action,
                schedule.Parameters.Values,
                schedule.Enabled,
                schedule.Notify.Key,
                participants.Select(participant => new ScheduleParticipantResponse(
                        participant.Kind.Key,
                        participant.Value,
                        participant.Label))
                    .ToList(),
                schedule.Cron.Occurrences(now, previewCount),
                state == DeletionState.Deleted);
    }

    extension(IScheduleRunQueries.ScheduleRunDetails run)
    {
        public ScheduleRunResponse ToResponse(DeletionState state) =>
            new(
                run.Id.Key,
                run.ScheduleId,
                run.ScheduleName,
                run.Action,
                run.ScheduledFor,
                run.StartedAt,
                run.FinishedAt,
                run.Status.Key,
                run.Summary,
                run.Link,
                run.Email.Key,
                run.Recipients,
                state == DeletionState.Deleted);
    }

    extension(IScheduledAction action)
    {
        public ScheduledActionResponse ToResponse() =>
            new(
                action.Descriptor.Key,
                action.Descriptor.NameKey,
                action.Descriptor.Parameters
                    .Select(parameter => new ScheduledActionParameterResponse(
                        parameter.Name,
                        parameter.LabelKey,
                        parameter.Kind.ToString().ToLowerInvariant(),
                        parameter.Required,
                        parameter.DefaultValue,
                        parameter.Placeholders,
                        parameter.Minimum,
                        parameter.Maximum))
                    .ToList());
    }

    extension(Validation<CronExpression> cron)
    {
        public ScheduleCronResponse ToResponse(DateTimeOffset now, int count) =>
            cron.Match(
                errors => new ScheduleCronResponse(
                    false,
                    string.Join("; ", errors.Select(error => error.Message)),
                    null,
                    []),
                expression => new ScheduleCronResponse(
                    true,
                    null,
                    new ScheduleCronFieldsResponse(
                        expression.Minute.Text,
                        expression.Hour.Text,
                        expression.DayOfMonth.Text,
                        expression.Month.Text,
                        expression.DayOfWeek.Text),
                    expression.Occurrences(now, count)));
    }

    extension(IScheduleParticipantQueries.ParticipantDirectory directory)
    {
        public ScheduleParticipantDirectoryResponse ToResponse() =>
            new(
                directory.Roles.Select(role => new ScheduleParticipantRoleResponse(role.Id, role.Name)).ToList(),
                directory.Users.Select(user => new ScheduleParticipantUserResponse(user.Id, user.Label)).ToList());
    }
}
