namespace Behind.Core.WebApi.Endpoints;

public sealed record AuditLogActor(string Kind, string Label, IReadOnlyList<string> Emails);

public sealed record AuditLogChange(string Property, string? OldValue, string? NewValue);

public sealed record AuditLogItem(
    DateTimeOffset Timestamp,
    string EntityName,
    string EntityKey,
    string Action,
    IReadOnlyList<AuditLogChange> Changes,
    AuditLogActor Actor,
    AuditLogActor? OnBehalfOf);
