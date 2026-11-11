using Behind.Common;
using Behind.Common.Database;
using Behind.Forum.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Forum.Database.Commands;

public sealed class ForumPostCommands(IForumContext context) : VersionedCommands<ForumPost, ForumPost>(context)
{
    protected override ForumPost Build(ForumPost draft) =>
        draft with { Title = draft.Title.Trim(), Body = draft.Body.Trim() };

    protected override async Task<Option<string>> Validate(Guid key, ForumPost draft, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(draft.Body) || draft.Body.Length > 20000 || draft.Title.Length > 200
            || draft.TopicId is null && string.IsNullOrWhiteSpace(draft.Title) || !Enum.IsDefined(draft.State))
        {
            return Some("invalid");
        }

        if (!await context.ForumBoards.OfKey(draft.BoardId).AnyAsync(cancellationToken))
        {
            return Some("unknownBoard");
        }

        if (draft.TopicId is { } topic
            && !await context.ForumPosts.LatestOf(context.ForumPosts).OfKey(topic)
                .AnyAsync(post => post.TopicId == null && post.BoardId == draft.BoardId
                                 && post.State == PublicationState.Published, cancellationToken))
        {
            return Some("unknownTopic");
        }

        return None;
    }

    protected override async Task<bool> IsReferenced(Guid key, CancellationToken cancellationToken)
    {
        using var scope = context.SoftDelete.IncludeDeleted();

        return await context.ForumPosts.AnyAsync(post => post.TopicId == key, cancellationToken);
    }
}
