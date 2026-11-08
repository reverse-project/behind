using Behind.Common.Database;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Sport.Database.Aggregates;

public sealed class StatTypeConfiguration : IEntityTypeConfiguration<StatType>
{
    public const string TableName = "SportStatTypes";

    public void Configure(EntityTypeBuilder<StatType> builder)
    {
        builder.ToTable(TableName);
        builder.HasVersionedKey();

        builder.Property(statType => statType.DisciplineId).IsRequired();
        builder.Property(statType => statType.Name).HasMaxLength(StatType.MaxNameLength).IsRequired();
        builder.Property(statType => statType.Abbreviation).HasMaxLength(StatType.MaxAbbreviationLength).IsRequired();
        builder.Property(statType => statType.Scope).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(statType => statType.HigherIsBetter).IsRequired();

        builder.HasIndex(statType => statType.DisciplineId);
    }
}
