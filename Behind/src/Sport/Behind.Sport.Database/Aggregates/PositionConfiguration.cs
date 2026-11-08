using Behind.Common.Database;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Sport.Database.Aggregates;

public sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public const string TableName = "SportPositions";

    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable(TableName);
        builder.HasVersionedKey();

        builder.Property(position => position.DisciplineId).IsRequired();
        builder.Property(position => position.Name).HasMaxLength(Position.MaxNameLength).IsRequired();
        builder.Property(position => position.Abbreviation).HasMaxLength(Position.MaxAbbreviationLength).IsRequired();
        builder.Property(position => position.SortOrder).IsRequired();

        builder.HasIndex(position => position.DisciplineId);
    }
}
