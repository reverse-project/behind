using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    public void Configure(EntityTypeBuilder<Menu> builder)
    {
        builder.ToTable("Menus");
        builder.HasVersionedKey();

        builder.Property(menu => menu.Name).HasMaxLength(Menu.MaxNameLength).IsRequired();
        builder.Property(menu => menu.Title).HasMaxLength(Menu.MaxTitleLength).IsRequired();

        builder.HasIndex(menu => menu.Name);
    }
}
