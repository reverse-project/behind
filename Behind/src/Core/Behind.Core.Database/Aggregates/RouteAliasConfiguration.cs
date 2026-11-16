using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class RouteAliasConfiguration : IEntityTypeConfiguration<RouteAlias>
{
    public void Configure(EntityTypeBuilder<RouteAlias> builder)
    {
        builder.ToTable("RouteAliases");
        builder.HasVersionedKey();

        builder.Property(alias => alias.Alias).HasMaxLength(RouteAlias.MaxAliasLength).IsRequired();
        builder.Property(alias => alias.Target).HasMaxLength(RouteAlias.MaxTargetLength).IsRequired();

        builder.HasIndex(alias => alias.Alias);
    }
}
