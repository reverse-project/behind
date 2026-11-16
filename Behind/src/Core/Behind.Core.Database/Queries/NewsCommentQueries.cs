using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class NewsCommentQueries(AbstractCoreContext context) : INewsCommentQueries
{
    public async Task<PagedResult<INewsCommentQueries.NewsCommentListing>> List(
        Guid newsId,
        Flipper flipper,
        Option<DateTimeOffset> activeAt = default,
        CancellationToken cancellationToken = default)
    {
        if (!await IsVisible(newsId, activeAt, cancellationToken))
        {
            return new PagedResult<INewsCommentQueries.NewsCommentListing>(0, flipper.Index, 0, flipper.Size, None);
        }

        var matching = context.ActiveComments()
            .Where(comment => EF.Property<Guid>(comment, NewsCommentConfiguration.NewsIdProperty) == newsId)
            .LatestOf(context.NewsComments);

        var total = await matching.LongCountAsync(cancellationToken);

        var rows = await flipper.Apply(
                context.OrderByCreation(matching, newestFirst: false)
                    .Select(comment => new CommentRow(
                        EF.Property<Guid>(comment, VersionedColumns.Key),
                        EF.Property<long>(comment, VersionedColumns.Version),
                        EF.Property<Guid>(comment, NewsCommentConfiguration.AuthorIdProperty),
                        comment.Content,
                        comment.IsAnonymous)))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var authors = await context.DisplayNamesAsync(rows.Select(row => row.AuthorId).Distinct().ToList(), cancellationToken);
        var times = await context.AuditTimesAsync<NewsComment>(
            rows.ToDictionary(row => row.Key, row => row.Version),
            cancellationToken);

        return new PagedResult<INewsCommentQueries.NewsCommentListing>(
            total,
            flipper.Index,
            flipper.PageCount(total),
            flipper.Size,
            rows.Count > 0
                ? Some<IEnumerable<INewsCommentQueries.NewsCommentListing>>(
                    rows.Select(row => new INewsCommentQueries.NewsCommentListing(
                        new VersionedKey<Guid>(row.Key, row.Version),
                        row.AuthorId,
                        authors.GetValueOrDefault(row.AuthorId, string.Empty),
                        row.Content,
                        times.TryGetValue(row.Key, out var commentTimes) ? commentTimes.CreatedAt : None,
                        times.TryGetValue(row.Key, out var editTimes) ? editTimes.UpdatedAt : None,
                        row.IsAnonymous)).ToList())
                : None);
    }

    public async Task<Option<Guid>> FindAuthor(Guid commentId, CancellationToken cancellationToken = default)
    {
        var authors = await context.ActiveComments()
            .OfKey(commentId)
            .LatestOf(context.NewsComments)
            .Select(comment => EF.Property<Guid>(comment, NewsCommentConfiguration.AuthorIdProperty))
            .ToListAsync(cancellationToken);

        return authors.Head();
    }

    private async Task<bool> IsVisible(Guid newsId, Option<DateTimeOffset> activeAt, CancellationToken cancellationToken) =>
        await activeAt.Match(
            () => Task.FromResult(true),
            now => context.LiveNews().ActiveAt(now).OfKey(newsId).AnyAsync(cancellationToken));

    private sealed record CommentRow(Guid Key, long Version, Guid AuthorId, string Content, bool IsAnonymous);
}
