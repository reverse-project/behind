using Behind.Common;

namespace Behind.Sport.Domain;

public sealed record PlayerSeason(
    VersionedKey<Guid> Id,
    Guid PlayerId,
    Guid SeasonId,
    Guid TeamId,
    int? ShirtNumber,
    Guid? PictureId)
{
    public const string EntityType = nameof(PlayerSeason);
    public const int MinShirtNumber = 0;
    public const int MaxShirtNumber = 999;

    public PlayerSeason(Guid playerId, Guid seasonId, Guid teamId, int? shirtNumber, Guid? pictureId)
        : this(new VersionedKey<Guid>(Guid.Empty), playerId, seasonId, teamId, shirtNumber, pictureId)
    {
    }
}
