using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public const string AuthorIdProperty = "AuthorId";
    public const string CategoryIdProperty = "CategoryId";

    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.ToTable("Articles");
        builder.HasVersionedKey();

        builder.Property(article => article.Title).HasMaxLength(Article.MaxTitleLength).IsRequired();
        builder.Property(article => article.Summary).IsRequired();
        builder.Property(article => article.Content).IsRequired();

        builder.Property<Guid>(AuthorIdProperty).IsRequired();
        builder.HasOne<HumanUser>()
            .WithMany()
            .HasForeignKey(AuthorIdProperty)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property<Guid?>(CategoryIdProperty).IsRequired(false);

        builder.HasIndex(CategoryIdProperty);
    }
}
