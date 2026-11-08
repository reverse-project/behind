using Behind.Common;

namespace Behind.Sport.Domain;

public sealed record Stat(VersionedKey<Guid> Id, Guid SeasonId, Guid StatTypeId, Guid SubjectId, double Value)
{
    public const string EntityType = nameof(Stat);
    public const double MaxValue = 1_000_000;

    public Stat(Guid seasonId, Guid statTypeId, Guid subjectId, double value)
        : this(new VersionedKey<Guid>(Guid.Empty), seasonId, statTypeId, subjectId, value)
    {
    }
}
