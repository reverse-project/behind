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
            builder.Property(h => h.DisplayName).HasMaxLength(DisplayNameExtensions.MaxLength).IsRequired();
            builder.HasIndex(h => h.DisplayName);
        });

    public static void ApplyUserAccess(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity("UserGrants", ug =>
        {
            ug.Property<Guid>("UserId");
            ug.Property<Guid>("GrantId");

            ug.HasKey("UserId", "GrantId");

            ug.HasOne(typeof(HumanUser))
                .WithMany()
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Cascade);

            ug.HasOne(typeof(Grant))
                .WithMany()
                .HasForeignKey("GrantId")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity("UserRestrictions", ur =>
        {
            ur.Property<Guid>("UserId");
            ur.Property<Guid>("RestrictionId");

            ur.HasKey("UserId", "RestrictionId");

            ur.HasOne(typeof(HumanUser))
                .WithMany()
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Cascade);

            ur.HasOne(typeof(Restriction))
                .WithMany()
                .HasForeignKey("RestrictionId")
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    public static void ApplyUserSettings(ModelBuilder modelBuilder) =>
        modelBuilder.Entity(UserSettingStore.Table, us =>
        {
            us.Property<Guid>(UserSettingStore.UserId);
            us.Property<string>(UserSettingStore.Key).HasMaxLength(UserSettingKeys.MaxKeyLength);
            us.Property<string>(UserSettingStore.Value).HasMaxLength(UserSettingKeys.MaxValueLength).IsRequired();

            us.HasKey(UserSettingStore.UserId, UserSettingStore.Key);

            us.HasOne(typeof(HumanUser))
                .WithMany()
                .HasForeignKey(UserSettingStore.UserId)
                .OnDelete(DeleteBehavior.Cascade);
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