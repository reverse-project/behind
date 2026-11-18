using Behind.Common.WebApi;

namespace Behind.Core.WebApi.Endpoints;

public sealed record ManagedSchedule(
    Guid Id,
    string Name,
    string Cron,
    string Action,
    bool Enabled,
    string Notify,
    DateTimeOffset? NextRunAt,
    bool IsDeleted);

public sealed record ScheduleManageResponse(PageResponse<ManagedSchedule> Schedules, IReadOnlyList<string> Letters);

public sealed record ScheduleParticipantResponse(string Kind, string Value, string Label);

public sealed record ScheduleDetailsResponse(
    Guid Id,
    long Version,
    string Name,
    string Cron,
    string Action,
    IReadOnlyDictionary<string, string> Parameters,
    bool Enabled,
    string Notify,
    IReadOnlyList<ScheduleParticipantResponse> Participants,
    IReadOnlyList<DateTimeOffset> NextRuns,
    bool IsDeleted);

public sealed record ScheduleRunStartedResponse(Guid RunId);

public sealed record ScheduledActionParameterResponse(
    string Name,
    string LabelKey,
    string Kind,
    bool Required,
    string? DefaultValue,
    IReadOnlyList<string> Placeholders,
    int? Minimum,
    int? Maximum);

public sealed record ScheduledActionResponse(
    string Key,
    string NameKey,
    IReadOnlyList<ScheduledActionParameterResponse> Parameters);

public sealed record ScheduleCronFieldsResponse(
    string Minute,
    string Hour,
    string DayOfMonth,
    string Month,
    string DayOfWeek);

public sealed record ScheduleCronResponse(
    bool Valid,
    string? Reason,
    ScheduleCronFieldsResponse? Fields,
    IReadOnlyList<DateTimeOffset> Next);

public sealed record ScheduleParticipantRoleResponse(Guid Id, string Name);

public sealed record ScheduleParticipantUserResponse(Guid Id, string Label);

public sealed record ScheduleParticipantDirectoryResponse(
    IReadOnlyList<ScheduleParticipantRoleResponse> Roles,
    IReadOnlyList<ScheduleParticipantUserResponse> Users);

public sealed record ScheduleRunResponse(
    Guid Id,
    Guid ScheduleId,
    string ScheduleName,
    string Action,
    DateTimeOffset ScheduledFor,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string Status,
    string Summary,
    string? Link,
    string Email,
    int Recipients,
    bool IsDeleted);
