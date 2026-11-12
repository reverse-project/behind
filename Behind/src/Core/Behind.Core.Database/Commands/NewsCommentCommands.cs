using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Commands;

public sealed class NewsCommentCommands(AbstractCoreContext context, TimeProvider timeProvider) : INewsCommentCommands
{
    public async Task<Option<Guid>> Add(
        Guid newsId,
        Guid authorId,
        string content,
        CancellationToken cancellationToken = default,
        bool isAnonymous = false)
    {
        var published = await context.LiveNews()
            .ActiveAt(timeProvider.GetUtcNow())
            .OfKey(newsId)
            .AnyAsync(cancellationToken);

        if (!published)
        {
            return None;
        }

        var key = Guid.CreateVersion7();

        return await AddVersion(key, 0, newsId, authorId, content, isAnonymous, cancellationToken) ? Some(key) : None;
    }

    public async Task<bool> Update(Guid commentId, string content, CancellationToken cancellationToken = default, bool? isAnonymous = null)
    {
        var latest = (await context.ActiveComments()
                .OfKey(commentId)
                .OrderByDescending(comment => EF.Property<long>(comment, VersionedColumns.Version))
                .Select(comment => new
                {
                    Version = EF.Property<long>(comment, VersionedColumns.Version),
                    NewsId = EF.Property<Guid>(comment, NewsCommentConfiguration.NewsIdProperty),
                    AuthorId = EF.Property<Guid>(comment, NewsCommentConfiguration.AuthorIdProperty),
                    comment.IsAnonymous
                })
                .Take(1)
                .ToListAsync(cancellationToken))
            .Head();

        return await latest.Match(
            () => Task.FromResult(false),
            found => AddVersion(commentId, found.Version, found.NewsId, found.AuthorId, content,
                isAnonymous ?? found.IsAnonymous, cancellationToken));
    }

    public async Task<bool> Delete(Guid commentId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.MarkSoftDeletedAsync(context.NewsComments.OfKey(commentId), cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return deleted > 0;
    }

    private async Task<bool> AddVersion(
        Guid key,
        long previousVersion,
        Guid newsId,
        Guid authorId,
        string content,
        bool isAnonymous,
        CancellationToken cancellationToken)
    {
        var comment = new NewsComment(content, isAnonymous);

        await context.NewsComments.AddAsync(comment, cancellationToken);

        var entry = context.Entry(comment);
        entry.StampVersionedKey(key, previousVersion + 1);
        entry.Property<Guid>(NewsCommentConfiguration.NewsIdProperty).CurrentValue = newsId;
        entry.Property<Guid>(NewsCommentConfiguration.AuthorIdProperty).CurrentValue = authorId;

        try
        {
            await context.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();

            return false;
        }
    }
}
