using Behind.Common.Database;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Sport.Database.Aggregates;

public sealed class DisciplineConfiguration : IEntityTypeConfiguration<Discipline>
{
    public const string TableName = "SportDisciplines";

    public void Configure(EntityTypeBuilder<Discipline> builder)
    {
        builder.ToTable(TableName);
        builder.HasVersionedKey();

        builder.Property(discipline => discipline.Name).HasMaxLength(Discipline.MaxNameLength).IsRequired();
        builder.Property(discipline => discipline.Slug).HasMaxLength(Discipline.MaxSlugLength).IsRequired();

        builder.HasIndex(discipline => discipline.Slug);
    }
}
