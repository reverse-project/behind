using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class NewsCommentConfiguration : IEntityTypeConfiguration<NewsComment>
{
    public const string NewsIdProperty = "NewsId";
    public const string AuthorIdProperty = "AuthorId";

    public void Configure(EntityTypeBuilder<NewsComment> builder)
    {
        builder.ToTable("NewsComments");
        builder.HasVersionedKey();

        builder.Property(comment => comment.Content).HasMaxLength(NewsComment.MaxContentLength).IsRequired();

        builder.Property<Guid>(NewsIdProperty).IsRequired();

        builder.Property<Guid>(AuthorIdProperty).IsRequired();
        builder.HasOne<HumanUser>()
            .WithMany()
            .HasForeignKey(AuthorIdProperty)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(NewsIdProperty);
    }
}
