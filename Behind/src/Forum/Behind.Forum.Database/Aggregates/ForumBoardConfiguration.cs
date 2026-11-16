using Behind.Common.Database;
using Behind.Forum.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Forum.Database.Aggregates;

public sealed class ForumBoardConfiguration : IEntityTypeConfiguration<ForumBoard>
{
    public void Configure(EntityTypeBuilder<ForumBoard> builder)
    {
        builder.ToTable("ForumBoards");
        builder.HasVersionedKey();
        builder.Property(board => board.Name).HasMaxLength(200).IsRequired();
        builder.Property(board => board.Description).HasMaxLength(2000).IsRequired();
        builder.HasIndex(board => board.CategoryId);
    }
}
