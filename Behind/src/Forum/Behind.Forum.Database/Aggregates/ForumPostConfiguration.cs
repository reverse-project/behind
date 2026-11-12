using Behind.Common.Database;
using Behind.Forum.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Forum.Database.Aggregates;

public sealed class ForumPostConfiguration : IEntityTypeConfiguration<ForumPost>
{
    public void Configure(EntityTypeBuilder<ForumPost> builder)
    {
        builder.ToTable("ForumPosts");
        builder.HasVersionedKey();
        builder.Property(post => post.Title).HasMaxLength(200).IsRequired();
        builder.Property(post => post.Body).HasMaxLength(20000).IsRequired();
        builder.Property(post => post.IsAnonymous).HasDefaultValue(false);
        builder.HasIndex(post => post.BoardId);
        builder.HasIndex(post => post.TopicId);
        builder.HasIndex(post => post.State);
    }
}
