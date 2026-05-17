using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Domain;

public sealed record AuditLogEntry(
    string EntityName,
    string EntityKey,
    string Action,
    User User,
    DateTimeOffset Timestamp,
    Option<User> BehalfOf
)
{
    public AuditLogEntry(
        string entityName,
        string entityKey,
        string action,
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