using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public const string MenuIdProperty = "MenuId";
    public const string ParentIdProperty = "ParentId";
    public const string RolesEntity = "MenuItemRoles";
    public const string RolesItemKeyProperty = "MenuItemKey";
    public const string RolesItemVersionProperty = "MenuItemVersion";
    public const string RolesRoleIdProperty = "RoleId";

    private const int EnumColumnLength = 20;

    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("MenuItems");
        builder.HasVersionedKey();

        builder.Property(item => item.Title).HasMaxLength(MenuItem.MaxTitleLength).IsRequired();
        builder.Property(item => item.Url).HasMaxLength(MenuItem.MaxUrlLength).IsRequired();
        builder.Property(item => item.Target).HasConversion<string>().HasMaxLength(EnumColumnLength).IsRequired();
        builder.Property(item => item.Kind).HasConversion<string>().HasMaxLength(EnumColumnLength).IsRequired();
        builder.Property(item => item.Position).IsRequired();

        builder.Property<Guid>(MenuIdProperty).IsRequired();
        builder.Property<Guid?>(ParentIdProperty).IsRequired(false);

        builder.HasIndex(MenuIdProperty);
        builder.HasIndex(ParentIdProperty);
    }

    public static void ApplyMenuItemRoles(ModelBuilder modelBuilder) =>
        modelBuilder.Entity(RolesEntity, roles =>
        {
            roles.Property<Guid>(RolesItemKeyProperty);
            roles.Property<long>(RolesItemVersionProperty);
            roles.Property<Guid>(RolesRoleIdProperty);

            roles.HasKey(RolesItemKeyProperty, RolesItemVersionProperty, RolesRoleIdProperty);

            roles.HasOne(typeof(MenuItem))
                .WithMany()
                .HasForeignKey(RolesItemKeyProperty, RolesItemVersionProperty)
                .HasPrincipalKey(VersionedColumns.Key, VersionedColumns.Version)
                .OnDelete(DeleteBehavior.Cascade);

            roles.HasOne(typeof(Role))
                .WithMany()
                .HasForeignKey(RolesRoleIdProperty)
                .OnDelete(DeleteBehavior.Restrict);
        });
}
