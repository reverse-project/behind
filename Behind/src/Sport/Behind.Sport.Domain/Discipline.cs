using Behind.Common;

namespace Behind.Sport.Domain;

public sealed record Discipline(VersionedKey<Guid> Id, string Name, string Slug)
{
    public const string EntityType = nameof(Discipline);
    public const int MaxNameLength = 100;
    public const int MaxSlugLength = 50;

    public Discipline(string name, string slug)
        : this(new VersionedKey<Guid>(Guid.Empty), name, slug)
    {
    }
}
