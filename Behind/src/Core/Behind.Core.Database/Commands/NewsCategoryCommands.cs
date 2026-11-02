using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Commands;

public sealed class NewsCategoryCommands(AbstractCoreContext context) : INewsCategoryCommands
{
    public Task<NewsCategorySaveResult> Create(
        INewsCategoryCommands.NewsCategoryDraft draft,
        CancellationToken cancellationToken = default) =>
        AddVersion(Guid.CreateVersion7(), 0, draft, cancellationToken);

    public async Task<NewsCategorySaveResult> Update(
        Guid categoryId,
        INewsCategoryCommands.NewsCategoryDraft draft,
        CancellationToken cancellationToken = default)
    {
        var latest = await context.ActiveCategories().OfKey(categoryId).LatestVersionAsync(categoryId, cancellationToken);

        return latest == 0
            ? new NewsCategorySaveResult.NotFound()
            : await AddVersion(categoryId, latest, draft, cancellationToken);
    }

    public async Task<bool> Delete(Guid categoryId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.MarkSoftDeletedAsync(context.NewsCategories.OfKey(categoryId), cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return deleted > 0;
    }

    public async Task<NewsCategorySaveResult> Restore(Guid categoryId, CancellationToken cancellationToken = default)
    {
        if (!await context.HasDeletedRowsAsync(context.NewsCategories.OfKey(categoryId), cancellationToken))
        {
            return new NewsCategorySaveResult.NotFound();
        }

        var name = await LatestName(categoryId, cancellationToken);

        if (await IsNameTaken(categoryId, name, cancellationToken))
        {
            return new NewsCategorySaveResult.NameTaken();
        }

        await context.MarkRestoredAsync(context.NewsCategories.OfKey(categoryId), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new NewsCategorySaveResult.Saved(new VersionedKey<Guid>(categoryId, await LatestVersion(categoryId, cancellationToken)));
    }

    public async Task<HardDeleteResult> HardDelete(Guid categoryId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.MarkHardDeletedAsync(context.NewsCategories.OfKey(categoryId), cancellationToken);

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

    private async Task<NewsCategorySaveResult> AddVersion(
        Guid key,
        long previousVersion,
        INewsCategoryCommands.NewsCategoryDraft draft,
        CancellationToken cancellationToken)
    {
        if (await IsNameTaken(key, draft.Name, cancellationToken))
        {
            return new NewsCategorySaveResult.NameTaken();
        }

        if (await IsImageMissing(draft, cancellationToken))
        {
            return new NewsCategorySaveResult.UnknownImage();
        }

        var id = new VersionedKey<Guid>(key, previousVersion + 1);
        var category = new NewsCategory(draft.Name);

        await context.NewsCategories.AddAsync(category, cancellationToken);

        var entry = context.Entry(category);
        entry.StampVersionedKey(id.Key, id.Version);
        entry.Property<Guid?>(NewsCategoryConfiguration.HeaderImageIdProperty).CurrentValue =
            draft.HeaderImageId.ToNullable();

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();

            return new NewsCategorySaveResult.VersionConflict();
        }

        return new NewsCategorySaveResult.Saved(id);
    }

    private Task<bool> IsImageMissing(
        INewsCategoryCommands.NewsCategoryDraft draft,
        CancellationToken cancellationToken) =>
        draft.HeaderImageId.Match(
            () => Task.FromResult(false),
            async imageId => !await context.SiteImages.AnyAsync(
                image => EF.Property<Guid>(image, "Id") == imageId,
                cancellationToken));

    private async Task<string> LatestName(Guid categoryId, CancellationToken cancellationToken)
    {
        using var scope = context.SoftDelete.IncludeDeleted();

        return await context.NewsCategories
            .OfKey(categoryId)
            .OrderByDescending(category => EF.Property<long>(category, VersionedColumns.Version))
            .Select(category => category.Name)
            .FirstAsync(cancellationToken);
    }

    private async Task<long> LatestVersion(Guid categoryId, CancellationToken cancellationToken)
    {
        using var scope = context.SoftDelete.IncludeDeleted();

        return await context.NewsCategories.LatestVersionAsync(categoryId, cancellationToken);
    }

    private async Task<bool> IsNameTaken(Guid categoryId, string name, CancellationToken cancellationToken)
    {
        var lowered = name.ToLower();

        return await context.ActiveCategories()
            .LatestOf(context.NewsCategories)
            .AnyAsync(
                category => category.Name.ToLower() == lowered
                            && EF.Property<Guid>(category, VersionedColumns.Key) != categoryId,
                cancellationToken);
    }
}
