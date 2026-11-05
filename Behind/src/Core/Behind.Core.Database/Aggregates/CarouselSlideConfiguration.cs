using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class CarouselSlideConfiguration : IEntityTypeConfiguration<CarouselSlide>
{
    public const string CarouselIdProperty = "CarouselId";
    public const string ImageIdProperty = "ImageId";
    public const string PositionProperty = "Position";

    public void Configure(EntityTypeBuilder<CarouselSlide> builder)
    {
        builder.ToTable("CarouselSlides");
        builder.HasVersionedKey();

        builder.Property(slide => slide.Caption).HasMaxLength(CarouselSlide.MaxCaptionLength).IsRequired();
        builder.Property(slide => slide.LinkUrl).HasMaxLength(CarouselSlide.MaxLinkUrlLength).IsRequired();

        builder.Property<Guid>(CarouselIdProperty).IsRequired();
        builder.Property<Guid>(ImageIdProperty).IsRequired();
        builder.Property<int>(PositionProperty).IsRequired();

        builder.HasIndex(CarouselIdProperty, VersionedColumns.Version);
        builder.HasIndex(ImageIdProperty);
    }
}
