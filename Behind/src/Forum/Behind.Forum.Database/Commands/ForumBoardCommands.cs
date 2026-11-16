using Behind.Common;
using Behind.Common.Database;
using Behind.Forum.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Forum.Database.Commands;

public sealed class ForumBoardCommands(IForumContext context) : VersionedCommands<ForumBoard, ForumBoard>(context)
{
    protected override ForumBoard Build(ForumBoard draft) =>
        draft with { Name = draft.Name.Trim(), Description = draft.Description.Trim() };

    protected override Task<Option<string>> Validate(Guid key, ForumBoard draft, CancellationToken cancellationToken) =>
        ValidateDraft(draft, cancellationToken);

    private async Task<Option<string>> ValidateDraft(ForumBoard draft, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(draft.Name) || draft.Name.Length > 200 || draft.Description.Length > 2000
            || draft.Moderation is { } mode && !Enum.IsDefined(mode))
        {
            return Some("invalid");
        }

        if (draft.CategoryId is { } categoryId
            && !await context.ForumBoardCategories.LatestOf(context.ForumBoardCategories).OfKey(categoryId)
                .AnyAsync(cancellationToken))
        {
            return Some("unknownCategory");
        }

        return None;
    }

    protected override async Task<bool> IsReferenced(Guid key, CancellationToken cancellationToken)
    {
        using var scope = context.SoftDelete.IncludeDeleted();

        return await context.ForumPosts.AnyAsync(post => post.BoardId == key, cancellationToken);
    }

}
