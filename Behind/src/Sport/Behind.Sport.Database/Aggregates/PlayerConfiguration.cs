using Behind.Common.Database;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Sport.Database.Aggregates;

public sealed class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public const string TableName = "SportPlayers";

    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.ToTable(TableName);
        builder.HasVersionedKey();

        builder.Property(player => player.DisciplineId).IsRequired();
        builder.Property(player => player.FirstName).HasMaxLength(Player.MaxNameLength).IsRequired();
        builder.Property(player => player.LastName).HasMaxLength(Player.MaxNameLength).IsRequired();
        builder.Property(player => player.BirthDate).IsRequired(false);
        builder.Property(player => player.Nationality).HasMaxLength(Player.MaxNationalityLength).IsRequired(false);
        builder.Property(player => player.PositionId).IsRequired(false);
        builder.Property(player => player.HeightCm).IsRequired(false);
        builder.Property(player => player.WeightKg).IsRequired(false);
        builder.Property(player => player.DominantSide).HasConversion<string>().HasMaxLength(20).IsRequired(false);
        builder.Property(player => player.Biography).HasMaxLength(Player.MaxBiographyLength).IsRequired(false);

        builder.HasIndex(player => player.DisciplineId);
        builder.HasIndex(player => player.PositionId);
    }
}
