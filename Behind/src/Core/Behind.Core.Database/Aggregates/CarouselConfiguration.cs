using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class CarouselConfiguration : IEntityTypeConfiguration<Carousel>
{
    public void Configure(EntityTypeBuilder<Carousel> builder)
    {
        builder.ToTable("Carousels");
        builder.HasVersionedKey();

        builder.Property(carousel => carousel.IntervalSeconds).IsRequired();
    }
}
