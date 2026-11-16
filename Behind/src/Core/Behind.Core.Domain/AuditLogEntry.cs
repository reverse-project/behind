using Behind.Common;
using System.ComponentModel.DataAnnotations.Schema;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Domain;

public sealed record AuditLogEntry(
    string EntityName,
    string EntityKey,
    AuditLogEntry.AuditAction Action,
    User User,
    DateTimeOffset Timestamp,
    Option<User> BehalfOf
)
{
    [NotMapped]
    public Guid? ActorId { get; init; }

    [NotMapped]
    public Guid? BehalfOfActorId { get; init; }

    public abstract record AuditAction;

    public sealed record Created : AuditAction;
    public sealed record Deleted : AuditAction;
    public sealed record HardDeleted : AuditAction;
    public sealed record Restored : AuditAction;
    public sealed record Updated(IEnumerable<PropertyDifference> Differences) : AuditAction;
    public sealed record ChangeOwnership(Guid OldOwnerId, Guid NewOwnerId) : AuditAction;
    public sealed record Moved(Guid OldParentId, Guid NewParentId) : AuditAction;
    public sealed record ImpersonationStarted : AuditAction;
    public sealed record ImpersonationEnded : AuditAction;
    
    public AuditLogEntry(
        string entityName,
        string entityKey,
        AuditAction action,
        DateTimeOffset timestamp
    ) : this(entityName,  entityKey, action, new GuestUser(None), timestamp, None)
    {
    }
    
    public AuditLogEntry(AuditLogEntry original) =>
    (
        EntityKey,
        EntityName,
        Action,
        Timestamp,
        User,
        BehalfOf
    ) = (
        original.EntityKey,
        original.EntityName,
        original.Action,
        original.Timestamp,
        new GuestUser(None),
        None
    );
}

public static class AuditLogEntryExtensions
{
    extension(AuditLogEntry auditLogEntry)
    {
        public AuditLogEntry WithUser(User user)
        {
            return auditLogEntry with { User = user };
        }
        public AuditLogEntry WithBehalfOf(User behalfOf)
        {
            return auditLogEntry with { BehalfOf = behalfOf };
        }
    }
}