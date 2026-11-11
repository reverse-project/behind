using Behind.Common.Database;
using Behind.Forum.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Forum.Database.Aggregates;

public sealed class ForumBoardCategoryConfiguration : IEntityTypeConfiguration<ForumBoardCategory>
{
    public void Configure(EntityTypeBuilder<ForumBoardCategory> builder)
    {
        builder.ToTable("ForumBoardCategories");
        builder.HasVersionedKey();
        builder.Property(category => category.Name).HasMaxLength(200).IsRequired();
    }
}
