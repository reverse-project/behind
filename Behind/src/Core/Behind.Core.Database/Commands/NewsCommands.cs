using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Commands;

public sealed class NewsCommands(AbstractCoreContext context, AbstractVersioningContext versions) : INewsCommands
{
    public Task<NewsSaveResult> Create(
        Guid authorId,
        INewsCommands.NewsDraft draft,
        bool publish,
        CancellationToken cancellationToken = default) =>
        AddVersion(Guid.CreateVersion7(), 0, authorId, draft, publish, cancellationToken);

    public async Task<NewsSaveResult> SaveVersion(
        Guid newsId,
        INewsCommands.NewsDraft draft,
        bool publish,
        CancellationToken cancellationToken = default)
    {
        var latest = (await context.ActiveNews()
                .OfKey(newsId)
                .OrderByDescending(news => EF.Property<long>(news, VersionedColumns.Version))
                .ToRows()
                .Take(1)
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .Head();

        return await latest.Match(
            () => Task.FromResult<NewsSaveResult>(new NewsSaveResult.NotFound()),
            row => AddVersion(newsId, row.Version, row.AuthorId, draft, publish, cancellationToken));
    }

    public async Task<NewsSaveResult> Activate(VersionedKey<Guid> id, CancellationToken cancellationToken = default)
    {
        var row = (await context.ActiveNews()
                .OfVersion(id)
                .ToRows()
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .Head();

        return await row.Match(
            () => Task.FromResult<NewsSaveResult>(new NewsSaveResult.NotFound()),
            async Task<NewsSaveResult> (found) =>
            {
                if (found.Highlighted)
                {
                    await Unhighlight(context.LiveNews().Where(NotKey(id.Key)), cancellationToken);
                }

                await SetActive(id, cancellationToken);

                return new NewsSaveResult.Saved(id);
            });
    }

    public async Task<bool> Delete(Guid newsId, CancellationToken cancellationToken = default)
    {
        await Unhighlight(context.LiveNews().OfKey(newsId), cancellationToken);

        var deleted = await context.MarkSoftDeletedAsync(context.NewsEntries.OfKey(newsId), cancellationToken);

        if (deleted == 0)
        {
            return false;
        }

        await context.MarkSoftDeletedAsync(CommentsOf(newsId), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> Restore(Guid newsId, CancellationToken cancellationToken = default)
    {
        var restored = await context.MarkRestoredAsync(context.NewsEntries.OfKey(newsId), cancellationToken);

        if (restored == 0)
        {
            return false;
        }

        await context.MarkRestoredAsync(CommentsOf(newsId), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<HardDeleteResult> HardDelete(Guid newsId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.MarkHardDeletedAsync(context.NewsEntries.OfKey(newsId), cancellationToken);

        if (deleted == 0)
        {
            return HardDeleteResult.NotFound;
        }

        await context.MarkHardDeletedAsync(CommentsOf(newsId), cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);

            await versions.ClearActiveVersionAsync(News.EntityType, newsId.ToString(), cancellationToken);
            await versions.SaveChangesAsync(cancellationToken);

            return HardDeleteResult.Deleted;
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();

            return HardDeleteResult.Rejected;
        }
    }

    private IQueryable<NewsComment> CommentsOf(Guid newsId) =>
        context.NewsComments.Where(comment =>
            EF.Property<Guid>(comment, NewsCommentConfiguration.NewsIdProperty) == newsId);

    private static System.Linq.Expressions.Expression<Func<News, bool>> NotKey(Guid key) =>
        news => EF.Property<Guid>(news, VersionedColumns.Key) != key;

    private async Task<NewsSaveResult> AddVersion(
        Guid key,
        long previousVersion,
        Guid authorId,
        INewsCommands.NewsDraft draft,
        bool publish,
        CancellationToken cancellationToken)
    {
        if (!await HasActiveCategory(draft, cancellationToken))
        {
            return new NewsSaveResult.UnknownCategory();
        }

        if (!draft.StartAt.IsValidSchedule(draft.EndAt))
        {
            return new NewsSaveResult.InvalidSchedule();
        }

        var id = new VersionedKey<Guid>(key, previousVersion + 1);

        if (publish && draft.Highlighted)
        {
            await Unhighlight(context.LiveNews().Where(NotKey(key)), cancellationToken);
        }

        var news = new News(draft.Title, draft.Summary, draft.Content, draft.Highlighted);

        await context.NewsEntries.AddAsync(news, cancellationToken);

        var entry = context.Entry(news);
        entry.StampVersionedKey(id.Key, id.Version);
        entry.Property<Guid>(NewsConfiguration.AuthorIdProperty).CurrentValue = authorId;
        entry.Property<Guid?>(NewsConfiguration.CategoryIdProperty).CurrentValue = draft.CategoryId.ToNullable();
        entry.Property<DateTimeOffset?>(NewsConfiguration.StartAtProperty).CurrentValue = draft.StartAt.ToNullable();
        entry.Property<DateTimeOffset?>(NewsConfiguration.EndAtProperty).CurrentValue = draft.EndAt.ToNullable();

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();

            return new NewsSaveResult.VersionConflict();
        }

        if (publish)
        {
            await SetActive(id, cancellationToken);
        }

        return new NewsSaveResult.Saved(id);
    }

    private async Task SetActive(VersionedKey<Guid> id, CancellationToken cancellationToken)
    {
        await versions.SetActiveVersionAsync(News.EntityType, id.Key.ToString(), id.Version, cancellationToken);
        await versions.SaveChangesAsync(cancellationToken);
    }

    private async Task Unhighlight(IQueryable<News> targets, CancellationToken cancellationToken)
    {
        var highlighted = await targets.Where(news => news.Highlighted).ToListAsync(cancellationToken);

        if (highlighted.Count == 0)
        {
            return;
        }

        highlighted.ForEach(news => context.Entry(news).Property(item => item.Highlighted).CurrentValue = false);
        await context.SaveChangesAsync(cancellationToken);
    }

    private Task<bool> HasActiveCategory(INewsCommands.NewsDraft draft, CancellationToken cancellationToken) =>
        draft.CategoryId.Match(
            () => Task.FromResult(true),
            categoryId => context.ActiveCategories().OfKey(categoryId).AnyAsync(cancellationToken));
}
