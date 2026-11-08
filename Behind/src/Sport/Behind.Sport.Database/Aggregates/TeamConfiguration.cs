using Behind.Common.Database;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Sport.Database.Aggregates;

public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public const string TableName = "SportTeams";

    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable(TableName);
        builder.HasVersionedKey();

        builder.Property(team => team.DisciplineId).IsRequired();
        builder.Property(team => team.Name).HasMaxLength(Team.MaxNameLength).IsRequired();
        builder.Property(team => team.ShortName).HasMaxLength(Team.MaxShortNameLength).IsRequired(false);
        builder.Property(team => team.City).HasMaxLength(Team.MaxCityLength).IsRequired(false);
        builder.Property(team => team.Country).HasMaxLength(Team.MaxCountryLength).IsRequired(false);
        builder.Property(team => team.FoundedYear).IsRequired(false);
        builder.Property(team => team.Description).HasMaxLength(Team.MaxDescriptionLength).IsRequired(false);
        builder.Property(team => team.LogoId).IsRequired(false);

        builder.HasIndex(team => team.DisciplineId);
    }
}
