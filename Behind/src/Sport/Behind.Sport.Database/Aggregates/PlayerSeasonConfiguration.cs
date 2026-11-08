using Behind.Common.Database;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Sport.Database.Aggregates;

public sealed class PlayerSeasonConfiguration : IEntityTypeConfiguration<PlayerSeason>
{
    public const string TableName = "SportPlayerSeasons";

    public void Configure(EntityTypeBuilder<PlayerSeason> builder)
    {
        builder.ToTable(TableName);
        builder.HasVersionedKey();

        builder.Property(playerSeason => playerSeason.PlayerId).IsRequired();
        builder.Property(playerSeason => playerSeason.SeasonId).IsRequired();
        builder.Property(playerSeason => playerSeason.TeamId).IsRequired();
        builder.Property(playerSeason => playerSeason.ShirtNumber).IsRequired(false);
        builder.Property(playerSeason => playerSeason.PictureId).IsRequired(false);

        builder.HasIndex(playerSeason => playerSeason.PlayerId);
        builder.HasIndex(playerSeason => playerSeason.SeasonId);
        builder.HasIndex(playerSeason => playerSeason.TeamId);
    }
}
