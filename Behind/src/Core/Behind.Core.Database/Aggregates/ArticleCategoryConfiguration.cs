using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class ArticleCategoryConfiguration : IEntityTypeConfiguration<ArticleCategory>
{
    public const string HeaderImageIdProperty = "HeaderImageId";

    public void Configure(EntityTypeBuilder<ArticleCategory> builder)
    {
        builder.ToTable("ArticleCategories");
        builder.HasVersionedKey();

        builder.Property(category => category.Name).HasMaxLength(ArticleCategory.MaxNameLength).IsRequired();

        builder.Property<Guid?>(HeaderImageIdProperty).IsRequired(false);
        builder.HasOne<SiteImage>()
            .WithMany()
            .HasForeignKey(HeaderImageIdProperty)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(category => category.Name);
    }
}
