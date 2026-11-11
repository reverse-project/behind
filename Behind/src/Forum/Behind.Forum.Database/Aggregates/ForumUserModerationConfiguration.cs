using Behind.Common.Database;
using Behind.Forum.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Forum.Database.Aggregates;

public sealed class ForumUserModerationConfiguration : IEntityTypeConfiguration<ForumUserModeration>
{
    public void Configure(EntityTypeBuilder<ForumUserModeration> builder)
    {
        builder.ToTable("ForumUserModerations");
        builder.HasVersionedKey();
        builder.HasIndex(rule => rule.UserId);
    }
}
