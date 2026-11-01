using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.UseTpcMappingStrategy();

        builder.Property<Guid>("Id").ValueGeneratedOnAdd();
        builder.HasKey("Id");

        builder.Ignore(r => r.Grants);
        builder.Ignore(r => r.Restrictions);
    }

    public static void ApplyRoleRestrictions(ModelBuilder modelBuilder) =>
        modelBuilder.Entity("RoleRestrictions", rr =>
        {
            rr.Property<Guid>("RoleId");
            rr.Property<Guid>("RestrictionId");

            rr.HasKey("RoleId", "RestrictionId");

            rr.HasOne(typeof(Role))
                .WithMany()
                .HasForeignKey("RoleId")
                .OnDelete(DeleteBehavior.Cascade);

            rr.HasOne(typeof(Restriction))
                .WithMany()
                .HasForeignKey("RestrictionId")
                .OnDelete(DeleteBehavior.Cascade);
        });

    public static void ApplyRoleGrants(ModelBuilder modelBuilder) =>
        modelBuilder.Entity("RoleGrants", rg =>
        {
            rg.Property<Guid>("RoleId");
            rg.Property<Guid>("GrantId");

            rg.HasKey("RoleId", "GrantId");

            rg.HasOne(typeof(Role))
                .WithMany()
                .HasForeignKey("RoleId")
                .OnDelete(DeleteBehavior.Cascade);

            rg.HasOne(typeof(Grant))
                .WithMany()
                .HasForeignKey("GrantId")
                .OnDelete(DeleteBehavior.Cascade);
        });
}
