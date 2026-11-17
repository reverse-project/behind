using Behind.Common.Database;
using Behind.Common.Scheduling;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IScheduleCommands : IVersionedCommands<IScheduleCommands.ScheduleDraft>
{
    public sealed record ScheduleDraft(
        string Name,
        CronExpression Cron,
        string Action,
        ScheduleParameters Parameters,
        bool Enabled,
        ScheduleNotification Notify,
        ScheduleParticipants Participants);
}
