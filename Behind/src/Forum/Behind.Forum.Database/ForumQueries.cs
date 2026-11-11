using Behind.Common;
using Behind.Common.Database;
using Behind.Common.Database.Modules;
using Behind.Forum.Common;
using Behind.Forum.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Forum.Database;

public sealed class ForumQueries(IForumContext context)
{
    public sealed record Page<T>(long Total, int Index, long PageCount, IReadOnlyList<Keyed<T>> Items);

    public async Task<IReadOnlyList<Keyed<ForumBoardCategory>>> ActiveCategories(CancellationToken cancellationToken) =>
        await context.ForumBoardCategories.LatestOf(context.ForumBoardCategories)
            .OrderBy(category => category.Name).ToKeyedAsync(cancellationToken);

    public async Task<Page<ForumBoardCategory>> Categories(
        Flipper page, DeletionState state, CancellationToken cancellationToken)
    {
        using var scope = state == DeletionState.Deleted ? context.SoftDelete.IncludeDeleted() : null;
        var rows = context.InDeletionState(context.ForumBoardCategories, state)
            .LatestOf(context.ForumBoardCategories);
        var total = await rows.LongCountAsync(cancellationToken);

        return new Page<ForumBoardCategory>(
            total, page.Index, page.PageCount(total),
            await page.Apply(rows.OrderBy(category => category.Name)).ToKeyedAsync(cancellationToken));
    }

    public async Task<bool> CategoryIsAssigned(Guid categoryId, CancellationToken cancellationToken)
    {
        using var scope = context.SoftDelete.IncludeDeleted();

        return await context.ForumBoards.LatestOf(context.ForumBoards)
            .AnyAsync(board => board.CategoryId == categoryId, cancellationToken);
    }

    public async Task<Page<ForumBoard>> Boards(Flipper page, DeletionState state, CancellationToken cancellationToken)
    {
        using var scope = state == DeletionState.Deleted ? context.SoftDelete.IncludeDeleted() : null;
        var rows = context.InDeletionState(context.ForumBoards, state).LatestOf(context.ForumBoards);
        var total = await rows.LongCountAsync(cancellationToken);
        var categories = context.ForumBoardCategories.LatestOf(context.ForumBoardCategories);

        return new Page<ForumBoard>(
            total, page.Index, page.PageCount(total),
            await page.Apply(rows.OrderBy(board => categories
                    .Where(category => EF.Property<Guid>(category, VersionedColumns.Key) == board.CategoryId)
                    .Select(category => category.Name).FirstOrDefault())
                .ThenBy(board => board.Name))
                .ToKeyedAsync(cancellationToken));
    }

    public async Task<Keyed<ForumBoard>?> Board(Guid id, CancellationToken cancellationToken) =>
        (await context.ForumBoards.LatestOf(context.ForumBoards).OfKey(id).ToKeyedAsync(cancellationToken)).SingleOrDefault();

    public async Task<Keyed<ForumPost>?> Post(Guid id, CancellationToken cancellationToken) =>
        (await context.ForumPosts.LatestOf(context.ForumPosts).OfKey(id).ToKeyedAsync(cancellationToken)).SingleOrDefault();

    public async Task<Page<ForumPost>> Posts(
        Flipper page,
        Guid? boardId,
        Guid? topicId,
        bool management,
        PublicationState? publication,
        DeletionState state,
        CancellationToken cancellationToken)
    {
        using var scope = state == DeletionState.Deleted ? context.SoftDelete.IncludeDeleted() : null;
        var rows = context.InDeletionState(context.ForumPosts, state).LatestOf(context.ForumPosts);

        if (!management)
        {
            rows = PublicPosts(rows).Where(post => post.TopicId == topicId);
        }

        if (boardId.HasValue)
        {
            rows = rows.Where(post => post.BoardId == boardId.Value);
        }

        if (management && publication.HasValue)
        {
            rows = rows.Where(post => post.State == publication.Value);
        }

        var total = await rows.LongCountAsync(cancellationToken);

        return new Page<ForumPost>(total, page.Index, page.PageCount(total),
            await page.Apply(rows.OrderBy(post => EF.Property<Guid>(post, VersionedColumns.Key)))
                .ToKeyedAsync(cancellationToken));
    }

    public async Task<Page<ForumPost>> LatestPosts(
        IModuleCreationOrder ordering, int limit, CancellationToken cancellationToken)
    {
        var rows = PublicPosts(context.ForumPosts.LatestOf(context.ForumPosts));
        var total = await rows.LongCountAsync(cancellationToken);
        var items = await ordering.NewestFirst(rows).Take(limit).ToKeyedAsync(cancellationToken);

        return new Page<ForumPost>(total, 1, 1, items);
    }

    private IQueryable<ForumPost> PublicPosts(IQueryable<ForumPost> rows)
    {
        var boards = context.ForumBoards.LatestOf(context.ForumBoards);
        var topics = context.ForumPosts.LatestOf(context.ForumPosts);

        return rows.Where(post => post.State == PublicationState.Published
            && boards.Any(board => EF.Property<Guid>(board, VersionedColumns.Key) == post.BoardId)
            && (post.TopicId == null || topics.Any(topic =>
                EF.Property<Guid>(topic, VersionedColumns.Key) == post.TopicId
                && topic.TopicId == null && topic.State == PublicationState.Published)));
    }

    public async Task<ModerationMode?> UserMode(Guid userId, CancellationToken cancellationToken) =>
        await context.ForumUserModerations.OfKey(userId).LatestOf(context.ForumUserModerations)
            .Select(rule => (ModerationMode?)rule.Moderation).SingleOrDefaultAsync(cancellationToken);

    public async Task<Page<ForumUserModeration>> UserRules(
        Flipper page, DeletionState state, CancellationToken cancellationToken)
    {
        using var scope = state == DeletionState.Deleted ? context.SoftDelete.IncludeDeleted() : null;
        var rows = context.InDeletionState(context.ForumUserModerations, state).LatestOf(context.ForumUserModerations);
        var total = await rows.LongCountAsync(cancellationToken);

        return new Page<ForumUserModeration>(total, page.Index, page.PageCount(total),
            await page.Apply(rows.OrderBy(rule => rule.UserId)).ToKeyedAsync(cancellationToken));
    }
}
