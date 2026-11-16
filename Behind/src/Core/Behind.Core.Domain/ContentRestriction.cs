using Behind.Common;

namespace Behind.Core.Domain;

public sealed record ContentRestriction(
    VersionedKey<Guid> Id,
    string EntityType,
    Guid EntityId,
    int MinimumAge)
{
    public ContentRestriction(string entityType, Guid entityId, int minimumAge)
        : this(new VersionedKey<Guid>(Guid.Empty), entityType, entityId, minimumAge)
    {
    }
}
