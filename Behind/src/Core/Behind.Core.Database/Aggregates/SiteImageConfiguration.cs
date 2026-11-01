using Behind.Core.Database.Converters;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class SiteImageConfiguration : IEntityTypeConfiguration<SiteImage>
{
    public const int MaxContentTypeLength = 100;

    public void Configure(EntityTypeBuilder<SiteImage> builder)
    {
        builder.Property<Guid>("Id").ValueGeneratedOnAdd();
        builder.HasKey("Id");

        builder.Property(image => image.FileName).HasMaxLength(SiteImage.MaxFileNameLength).IsRequired();
        builder.Property(image => image.ContentType).HasMaxLength(MaxContentTypeLength).IsRequired();
        builder.Property(image => image.Content).IsRequired();
        builder.Property(image => image.CreatedAt).HasConversion<DateTimeOffsetConverter>();
    }
}
