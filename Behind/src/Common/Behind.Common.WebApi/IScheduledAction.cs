using System.Security.Claims;
using Behind.Common;

namespace Behind.Common.WebApi;

public enum ScheduledActionParameterKind
{
    Text,
    Choice,
    Template,
    Number
}

public sealed record ScheduledActionParameter(
    string Name,
    string LabelKey,
    ScheduledActionParameterKind Kind,
    bool Required)
{
    public string? DefaultValue { get; init; }

    public IReadOnlyList<string> Placeholders { get; init; } = [];

    public int? Minimum { get; init; }

    public int? Maximum { get; init; }
}

public sealed record ScheduledActionDescriptor(
    string Key,
    string NameKey,
    IReadOnlyList<ScheduledActionParameter> Parameters);

public sealed record ScheduledActionOption(string Value, string Label);

public sealed record ScheduledActionLink(string Route, IReadOnlyDictionary<string, string?> Query);

public sealed record ScheduledActionInvocation(
    Guid ScheduleId,
    string ScheduleName,
    DateTimeOffset ScheduledFor,
    IReadOnlyDictionary<string, string> Parameters);

public sealed record ScheduledActionResult(bool Succeeded, string Summary, Option<ScheduledActionLink> Link);

public interface IScheduledAction
{
    ScheduledActionDescriptor Descriptor { get; }

    Task<IReadOnlyList<ScheduledActionOption>> Options(
        ClaimsPrincipal user,
        string parameter,
        CancellationToken cancellationToken);

    Task<Option<string>> Validate(
        ClaimsPrincipal user,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken);

    Task<ScheduledActionResult> Run(ScheduledActionInvocation invocation, CancellationToken cancellationToken);
}
