using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public sealed class ArticleCategoryCommands(AbstractCoreContext context) : IArticleCategoryCommands
{
    public Task<ArticleCategorySaveResult> Create(
        IArticleCategoryCommands.ArticleCategoryDraft draft,
        CancellationToken cancellationToken = default) =>
        AddVersion(Guid.CreateVersion7(), 0, draft, cancellationToken);

    public async Task<ArticleCategorySaveResult> Update(
        Guid categoryId,
        IArticleCategoryCommands.ArticleCategoryDraft draft,
        CancellationToken cancellationToken = default)
    {
        var latest = await context.ActiveArticleCategories()
            .OfKey(categoryId)
            .LatestVersionAsync(categoryId, cancellationToken);

        return latest == 0
            ? new ArticleCategorySaveResult.NotFound()
            : await AddVersion(categoryId, latest, draft, cancellationToken);
    }

    public async Task<bool> Delete(Guid categoryId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.MarkSoftDeletedAsync(
            context.ArticleCategories.OfKey(categoryId),
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return deleted > 0;
    }

    public async Task<ArticleCategorySaveResult> Restore(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        if (!await context.HasDeletedRowsAsync(context.ArticleCategories.OfKey(categoryId), cancellationToken))
        {
            return new ArticleCategorySaveResult.NotFound();
        }

        var name = await LatestName(categoryId, cancellationToken);

        if (await IsNameTaken(categoryId, name, cancellationToken))
        {
            return new ArticleCategorySaveResult.NameTaken();
        }

        await context.MarkRestoredAsync(context.ArticleCategories.OfKey(categoryId), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new ArticleCategorySaveResult.Saved(
            new VersionedKey<Guid>(categoryId, await LatestVersion(categoryId, cancellationToken)));
    }

    public async Task<HardDeleteResult> HardDelete(Guid categoryId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.MarkHardDeletedAsync(
            context.ArticleCategories.OfKey(categoryId),
            cancellationToken);

        if (deleted == 0)
        {
            return HardDeleteResult.NotFound;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);

            return HardDeleteResult.Deleted;
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();

            return HardDeleteResult.Rejected;
        }
    }

    private async Task<ArticleCategorySaveResult> AddVersion(
        Guid key,
        long previousVersion,
        IArticleCategoryCommands.ArticleCategoryDraft draft,
        CancellationToken cancellationToken)
    {
        if (await IsNameTaken(key, draft.Name, cancellationToken))
        {
            return new ArticleCategorySaveResult.NameTaken();
        }

        if (await IsImageMissing(draft, cancellationToken))
        {
            return new ArticleCategorySaveResult.UnknownImage();
        }

        var id = new VersionedKey<Guid>(key, previousVersion + 1);
        var category = new ArticleCategory(draft.Name);

        await context.ArticleCategories.AddAsync(category, cancellationToken);

        var entry = context.Entry(category);
        entry.StampVersionedKey(id.Key, id.Version);
        entry.Property<Guid?>(ArticleCategoryConfiguration.HeaderImageIdProperty).CurrentValue =
            draft.HeaderImageId.ToNullable();

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();

            return new ArticleCategorySaveResult.VersionConflict();
        }

        return new ArticleCategorySaveResult.Saved(id);
    }

    private Task<bool> IsImageMissing(
        IArticleCategoryCommands.ArticleCategoryDraft draft,
        CancellationToken cancellationToken) =>
        draft.HeaderImageId.Match(
            () => Task.FromResult(false),
            async imageId => !await context.SiteImages.AnyAsync(
                image => EF.Property<Guid>(image, "Id") == imageId,
                cancellationToken));

    private async Task<string> LatestName(Guid categoryId, CancellationToken cancellationToken)
    {
        using var scope = context.SoftDelete.IncludeDeleted();

        return await context.ArticleCategories
            .OfKey(categoryId)
            .OrderByDescending(category => EF.Property<long>(category, VersionedColumns.Version))
            .Select(category => category.Name)
            .FirstAsync(cancellationToken);
    }

    private async Task<long> LatestVersion(Guid categoryId, CancellationToken cancellationToken)
    {
        using var scope = context.SoftDelete.IncludeDeleted();

        return await context.ArticleCategories.LatestVersionAsync(categoryId, cancellationToken);
    }

    private async Task<bool> IsNameTaken(Guid categoryId, string name, CancellationToken cancellationToken)
    {
        var lowered = name.ToLower();

        return await context.ActiveArticleCategories()
            .LatestOf(context.ArticleCategories)
            .AnyAsync(
                category => category.Name.ToLower() == lowered
                            && EF.Property<Guid>(category, VersionedColumns.Key) != categoryId,
                cancellationToken);
    }
}
