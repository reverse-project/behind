using Behind.Common;

namespace Behind.Core.Domain;

public sealed record ContentLocale(VersionedKey<Guid> Id, string EntityType, Guid EntityId, string? Locale)
{
    public const string TypeName = nameof(ContentLocale);

    public ContentLocale(string entityType, Guid entityId, string? locale)
        : this(new VersionedKey<Guid>(Guid.Empty), entityType, entityId, locale)
    {
    }
}
