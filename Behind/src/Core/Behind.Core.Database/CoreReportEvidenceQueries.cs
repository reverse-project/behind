using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public sealed class CoreReportEvidenceQueries(AbstractCoreContext context)
{
    public sealed record Evidence(Guid AuthorId, string Content);

    public async Task<Evidence?> Find(
        string sourceType, Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (sourceType == "profile")
        {
            return await context.Users.Where(user => EF.Property<Guid>(user, "Id") == id)
                .Select(user => new Evidence(id, user.DisplayName))
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        }

        if (sourceType == "chatMessage")
        {
            return await context.ChatMessages.LatestOf(context.ChatMessages).OfKey(id)
                .Select(message => new Evidence(message.AuthorId, message.Text))
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        }

        if (sourceType == "newsComment")
        {
            var news = context.LiveNews().ActiveAt(now);
            return await context.ActiveComments().LatestOf(context.NewsComments).OfKey(id)
                .Where(comment => news.Any(item =>
                    EF.Property<Guid>(item, VersionedColumns.Key)
                    == EF.Property<Guid>(comment, NewsCommentConfiguration.NewsIdProperty)))
                .Select(comment => new Evidence(
                    EF.Property<Guid>(comment, NewsCommentConfiguration.AuthorIdProperty), comment.Content))
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        }

        return null;
    }
}
