using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class SiteSectionConfiguration : IEntityTypeConfiguration<SiteSection>
{
    public const string SiteIdProperty = "SiteId";
    public const string ImageIdProperty = "ImageId";
    public const string PositionProperty = "Position";

    private const int EnumColumnLength = 20;

    public void Configure(EntityTypeBuilder<SiteSection> builder)
    {
        builder.ToTable("SiteSections");
        builder.HasVersionedKey();

        builder.Property(section => section.Theme).HasConversion<string>().HasMaxLength(EnumColumnLength).IsRequired();
        builder.Property(section => section.Title).HasMaxLength(SiteSection.MaxTitleLength).IsRequired();
        builder.Property(section => section.Content).IsRequired();
        builder.Property(section => section.ImageAlt).HasMaxLength(SiteSection.MaxImageAltLength).IsRequired();
        builder.Property(section => section.ImagePosition)
            .HasConversion<string>()
            .HasMaxLength(EnumColumnLength)
            .IsRequired();

        builder.Property<Guid>(SiteIdProperty).IsRequired();
        builder.Property<Guid?>(ImageIdProperty).IsRequired(false);
        builder.Property<int>(PositionProperty).IsRequired();

        builder.HasIndex(SiteIdProperty, VersionedColumns.Version);
        builder.HasIndex(ImageIdProperty);
    }
}
