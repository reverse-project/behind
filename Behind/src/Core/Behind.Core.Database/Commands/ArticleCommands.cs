using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public sealed class ArticleCommands(
    AbstractCoreContext context,
    AbstractVersioningContext versions,
    IVfsCommands files) : IArticleCommands
{
    public Task<ArticleSaveResult> Create(
        Guid authorId,
        IArticleCommands.ArticleDraft draft,
        bool publish,
        CancellationToken cancellationToken = default) =>
        AddVersion(Guid.CreateVersion7(), 0, authorId, draft, publish, cancellationToken);

    public async Task<ArticleSaveResult> SaveVersion(
        Guid articleId,
        IArticleCommands.ArticleDraft draft,
        bool publish,
        CancellationToken cancellationToken = default)
    {
        var latest = (await context.ActiveArticles()
                .OfKey(articleId)
                .OrderByDescending(article => EF.Property<long>(article, VersionedColumns.Version))
                .ToRows()
                .Take(1)
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .Head();

        return await latest.Match(
            () => Task.FromResult<ArticleSaveResult>(new ArticleSaveResult.NotFound()),
            row => AddVersion(articleId, row.Version, row.AuthorId, draft, publish, cancellationToken));
    }

    public async Task<ArticleSaveResult> Activate(
        VersionedKey<Guid> id,
        CancellationToken cancellationToken = default)
    {
        var exists = await context.ActiveArticles().OfVersion(id).AnyAsync(cancellationToken);

        if (!exists)
        {
            return new ArticleSaveResult.NotFound();
        }

        await SetActive(id, cancellationToken);

        return new ArticleSaveResult.Saved(id);
    }

    public async Task<bool> Delete(Guid articleId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.MarkSoftDeletedAsync(context.Articles.OfKey(articleId), cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return deleted > 0;
    }

    public async Task<bool> Restore(Guid articleId, CancellationToken cancellationToken = default)
    {
        var restored = await context.MarkRestoredAsync(context.Articles.OfKey(articleId), cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return restored > 0;
    }

    public async Task<HardDeleteResult> HardDelete(Guid articleId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.MarkHardDeletedAsync(context.Articles.OfKey(articleId), cancellationToken);

        if (deleted == 0)
        {
            return HardDeleteResult.NotFound;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);

            await versions.ClearActiveVersionAsync(Article.EntityType, articleId.ToString(), cancellationToken);
            await versions.SaveChangesAsync(cancellationToken);

            return HardDeleteResult.Deleted;
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();

            return HardDeleteResult.Rejected;
        }
    }

    private async Task<ArticleSaveResult> AddVersion(
        Guid key,
        long previousVersion,
        Guid authorId,
        IArticleCommands.ArticleDraft draft,
        bool publish,
        CancellationToken cancellationToken)
    {
        if (!await HasActiveCategory(draft, cancellationToken))
        {
            return new ArticleSaveResult.UnknownCategory();
        }

        var id = new VersionedKey<Guid>(key, previousVersion + 1);
        var article = new Article(draft.Title, draft.Summary, draft.Content);

        await context.Articles.AddAsync(article, cancellationToken);

        var entry = context.Entry(article);
        entry.StampVersionedKey(id.Key, id.Version);
        entry.Property<Guid>(ArticleConfiguration.AuthorIdProperty).CurrentValue = authorId;
        entry.Property<Guid?>(ArticleConfiguration.CategoryIdProperty).CurrentValue = draft.CategoryId.ToNullable();

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();

            return new ArticleSaveResult.VersionConflict();
        }

        await files.AttachSiteImagesOf(VfsTypes.Articles, id.Key, [draft.Summary, draft.Content], cancellationToken);

        if (publish)
        {
            await SetActive(id, cancellationToken);
        }

        return new ArticleSaveResult.Saved(id);
    }

    private async Task SetActive(VersionedKey<Guid> id, CancellationToken cancellationToken)
    {
        await versions.SetActiveVersionAsync(Article.EntityType, id.Key.ToString(), id.Version, cancellationToken);
        await versions.SaveChangesAsync(cancellationToken);
    }

    private Task<bool> HasActiveCategory(IArticleCommands.ArticleDraft draft, CancellationToken cancellationToken) =>
        draft.CategoryId.Match(
            () => Task.FromResult(true),
            categoryId => context.ActiveArticleCategories().OfKey(categoryId).AnyAsync(cancellationToken));
}
