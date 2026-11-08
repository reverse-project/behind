using Behind.Common.Database;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Sport.Database.Aggregates;

public sealed class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public const string TableName = "SportSeasons";

    public void Configure(EntityTypeBuilder<Season> builder)
    {
        builder.ToTable(TableName);
        builder.HasVersionedKey();

        builder.Property(season => season.DisciplineId).IsRequired();
        builder.Property(season => season.Name).HasMaxLength(Season.MaxNameLength).IsRequired();
        builder.Property(season => season.StartsOn).IsRequired(false);
        builder.Property(season => season.EndsOn).IsRequired(false);

        builder.HasIndex(season => season.DisciplineId);
    }
}
