using Behind.Common;
using Behind.Common.Database;
using Behind.Forum.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Forum.Database.Commands;

public sealed class ForumBoardCategoryCommands(IForumContext context)
    : VersionedCommands<ForumBoardCategory, ForumBoardCategory>(context)
{
    protected override ForumBoardCategory Build(ForumBoardCategory draft) => draft with { Name = draft.Name.Trim() };

    protected override async Task<Option<string>> Validate(
        Guid key, ForumBoardCategory draft, CancellationToken cancellationToken)
    {
        var name = draft.Name.Trim();

        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            return Some("invalid");
        }

        using var scope = context.SoftDelete.IncludeDeleted();
        var nameTaken = await context.ForumBoardCategories.LatestOf(context.ForumBoardCategories)
            .AnyAsync(category => category.Name == name
                && EF.Property<Guid>(category, VersionedColumns.Key) != key, cancellationToken);

        return nameTaken ? Some("nameTaken") : None;
    }

    protected override async Task<bool> IsReferenced(Guid key, CancellationToken cancellationToken)
    {
        using var scope = context.SoftDelete.IncludeDeleted();

        return await context.ForumBoards.LatestOf(context.ForumBoards)
            .AnyAsync(board => board.CategoryId == key, cancellationToken);
    }

    protected override async Task<bool> CanDelete(Guid key, CancellationToken cancellationToken) =>
        !await IsReferenced(key, cancellationToken);
}
