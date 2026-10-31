using Behind.Common;
using Behind.Core.Domain;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Auditing;

public sealed record AuditActor(Option<Guid> UserId, Option<Guid> BehalfOfId)
{
    public static AuditActor Autonomous { get; } = new(Some(DomainConstants.SystemUserId), None);

    public static AuditActor Anonymous { get; } = new(None, None);

    public static AuditActor ForUser(Guid userId) => new(Some(userId), None);
}
