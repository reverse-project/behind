using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class GalleryItemConfiguration : IEntityTypeConfiguration<GalleryItem>
{
    public const string GalleryIdProperty = "GalleryId";
    public const string ImageIdProperty = "ImageId";
    public const string ThumbnailIdProperty = "ThumbnailId";
    public const string PositionProperty = "Position";

    public void Configure(EntityTypeBuilder<GalleryItem> builder)
    {
        builder.ToTable("GalleryItems");
        builder.HasVersionedKey();

        builder.Property(item => item.Description).HasMaxLength(GalleryItem.MaxDescriptionLength).IsRequired();

        builder.Property<Guid>(GalleryIdProperty).IsRequired();
        builder.Property<int>(PositionProperty).IsRequired();

        builder.Property<Guid>(ImageIdProperty).IsRequired();
        builder.Property<Guid>(ThumbnailIdProperty).IsRequired();

        builder.HasIndex(GalleryIdProperty, VersionedColumns.Version);
        builder.HasIndex(ImageIdProperty);
        builder.HasIndex(ThumbnailIdProperty);
    }
}
