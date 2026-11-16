using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class NewsCategoryConfiguration : IEntityTypeConfiguration<NewsCategory>
{
    public const string HeaderImageIdProperty = "HeaderImageId";

    public void Configure(EntityTypeBuilder<NewsCategory> builder)
    {
        builder.ToTable("NewsCategories");
        builder.HasVersionedKey();

        builder.Property(category => category.Name).HasMaxLength(NewsCategory.MaxNameLength).IsRequired();

        builder.Property<Guid?>(HeaderImageIdProperty).IsRequired(false);

        builder.HasIndex(category => category.Name);
    }
}
