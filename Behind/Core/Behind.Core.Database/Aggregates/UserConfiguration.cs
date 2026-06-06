using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.UseTpcMappingStrategy();

        builder.Property<Guid>("Id").ValueGeneratedOnAdd();
        builder.HasKey("Id");

        builder.Ignore(u => u.Roles);
    }

    public static void ApplyHumanUser(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<HumanUser>(builder =>
        {
            builder.Property(h => h.DisplayName).HasMaxLength(500).IsRequired();
            builder.HasIndex(h => h.DisplayName);
        });

    public static void ApplyUserRoles(ModelBuilder modelBuilder) =>
        modelBuilder.Entity("UserRoles", ur =>
        {
            ur.Property<Guid>("RoleId");
            ur.Property<Guid>("UserId");

            ur.HasKey("RoleId", "UserId");

            ur.HasOne(typeof(Role))
                .WithMany()
                .HasForeignKey("RoleId")
                .OnDelete(DeleteBehavior.Cascade);

            ur.HasOne(typeof(HumanUser))
                .WithMany()
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Cascade);
        });
}