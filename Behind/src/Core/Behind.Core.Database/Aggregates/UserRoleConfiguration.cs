using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("Roles");

        builder.HasIndex(r => r.Name);
        builder.Property<string>("NormalizedName")
            .UseCollation("C")
            .HasComputedColumnSql("\"BehindOrdinalIgnoreCaseKey\"(\"Name\")", stored: true);
        builder.HasIndex("NormalizedName")
            .IsUnique()
            .HasDatabaseName("UX_Roles_NormalizedName");
    }
}
