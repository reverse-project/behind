using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class GalleryConfiguration : IEntityTypeConfiguration<Gallery>
{
    public const string AuthorIdProperty = "AuthorId";
    public const string ThumbnailImageIdProperty = "ThumbnailImageId";
    public const string ThumbnailGeneratedProperty = "ThumbnailGenerated";

    public void Configure(EntityTypeBuilder<Gallery> builder)
    {
        builder.ToTable("Galleries");
        builder.HasVersionedKey();

        builder.Property(gallery => gallery.Title).HasMaxLength(Gallery.MaxTitleLength).IsRequired();
        builder.Property(gallery => gallery.Description).IsRequired();

        builder.Property<Guid>(AuthorIdProperty).IsRequired();
        builder.HasOne<HumanUser>()
            .WithMany()
            .HasForeignKey(AuthorIdProperty)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property<Guid?>(ThumbnailImageIdProperty).IsRequired(false);

        builder.Property<bool>(ThumbnailGeneratedProperty).IsRequired();

        builder.HasIndex(gallery => gallery.Title);
    }
}
