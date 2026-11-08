using Behind.Common.Database;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Sport.Database.Aggregates;

public sealed class StatConfiguration : IEntityTypeConfiguration<Stat>
{
    public const string TableName = "SportStats";

    public void Configure(EntityTypeBuilder<Stat> builder)
    {
        builder.ToTable(TableName);
        builder.HasVersionedKey();

        builder.Property(stat => stat.SeasonId).IsRequired();
        builder.Property(stat => stat.StatTypeId).IsRequired();
        builder.Property(stat => stat.SubjectId).IsRequired();
        builder.Property(stat => stat.Value).IsRequired();

        builder.HasIndex(stat => new { stat.SeasonId, stat.StatTypeId });
        builder.HasIndex(stat => stat.SubjectId);
    }
}
