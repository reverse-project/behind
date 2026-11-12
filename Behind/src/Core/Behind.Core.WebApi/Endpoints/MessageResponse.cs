using Behind.Common.WebApi;

namespace Behind.Core.WebApi.Endpoints;

public sealed record ManagedMessage(
    Guid Id,
    string Name,
    string Title,
    string Level,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    bool IsDeleted);

public sealed record MessageManageResponse(PageResponse<ManagedMessage> Messages, IReadOnlyList<string> Letters);

public sealed record MessageDetailsResponse(
    Guid Id,
    string Name,
    string Title,
    string Content,
    string Level,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    bool IsDeleted);

public sealed record MessageWidgetResponse(
    Guid Id,
    long Version,
    string Title,
    string Content,
    string Level,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt);
