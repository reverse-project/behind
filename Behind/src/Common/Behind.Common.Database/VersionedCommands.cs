using Behind.Common.Database.Modules;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common.Database;

public abstract class VersionedCommands<TEntity, TDraft>(IModuleContext context) : IVersionedCommands<TDraft>
    where TEntity : class
{
    protected DbContext Database => context.Context;

    protected IModuleContext Module => context;

    protected IQueryable<TEntity> Rows => Database.Set<TEntity>();

    protected abstract TEntity Build(TDraft draft);

    protected virtual Task<Option<string>> Validate(
        Guid key,
        TDraft draft,
        CancellationToken cancellationToken) =>
        Task.FromResult<Option<string>>(None);

    protected virtual Task<bool> IsReferenced(Guid key, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    protected virtual Task<bool> CanDelete(Guid key, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    protected virtual Task Saved(VersionedKey<Guid> id, TDraft draft, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<VersionedSaveResult> Create(TDraft draft, CancellationToken cancellationToken = default) =>
        AddVersion(Guid.CreateVersion7(), 0, draft, cancellationToken);

    public async Task<VersionedSaveResult> Update(
        Guid key,
        TDraft draft,
        CancellationToken cancellationToken = default)
    {
        var latest = await Rows.LatestVersionAsync(key, cancellationToken);

        return latest == 0
            ? new VersionedSaveResult.NotFound()
            : await AddVersion(key, latest, draft, cancellationToken);
    }

    public async Task<bool> Delete(Guid key, CancellationToken cancellationToken = default)
    {
        if (!await CanDelete(key, cancellationToken))
        {
            return false;
        }

        var deleted = await Database.MarkSoftDeletedAsync(Rows.OfKey(key), cancellationToken);

        await Database.SaveChangesAsync(cancellationToken);

        return deleted > 0;
    }

    public async Task<bool> Restore(Guid key, CancellationToken cancellationToken = default)
    {
        var restored = await Database.MarkRestoredAsync(Rows.OfKey(key), cancellationToken);

        await Database.SaveChangesAsync(cancellationToken);

        return restored > 0;
    }

    public async Task<HardDeleteResult> HardDelete(Guid key, CancellationToken cancellationToken = default)
    {
        if (!await Database.HasDeletedRowsAsync(Rows.OfKey(key), cancellationToken))
        {
            return HardDeleteResult.NotFound;
        }

        if (await IsReferenced(key, cancellationToken))
        {
            return HardDeleteResult.Rejected;
        }

        await Database.MarkHardDeletedAsync(Rows.OfKey(key), cancellationToken);

        try
        {
            await Database.SaveChangesAsync(cancellationToken);

            return HardDeleteResult.Deleted;
        }
        catch (DbUpdateException)
        {
            Database.ChangeTracker.Clear();

            return HardDeleteResult.Rejected;
        }
    }

    public async Task<bool> Ensure(Guid key, TDraft draft, CancellationToken cancellationToken = default) =>
        !await ExistsInAnyState(key, cancellationToken)
        && await AddVersion(key, 0, draft, cancellationToken) is VersionedSaveResult.Saved;

    private async Task<bool> ExistsInAnyState(Guid key, CancellationToken cancellationToken)
    {
        using var scope = context.SoftDelete.IncludeDeleted();

        return await Rows.OfKey(key).AnyAsync(cancellationToken);
    }

    private async Task<VersionedSaveResult> AddVersion(
        Guid key,
        long previousVersion,
        TDraft draft,
        CancellationToken cancellationToken) =>
        await (await Validate(key, draft, cancellationToken)).Match(
            () => Persist(key, previousVersion, draft, cancellationToken),
            reason => Task.FromResult<VersionedSaveResult>(new VersionedSaveResult.Rejected(reason)));

    private async Task<VersionedSaveResult> Persist(
        Guid key,
        long previousVersion,
        TDraft draft,
        CancellationToken cancellationToken)
    {
        var id = new VersionedKey<Guid>(key, previousVersion + 1);
        var entity = Build(draft);

        await Database.Set<TEntity>().AddAsync(entity, cancellationToken);
        Database.Entry(entity).StampVersionedKey(id.Key, id.Version);

        try
        {
            await Database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            Database.ChangeTracker.Clear();

            return new VersionedSaveResult.VersionConflict();
        }

        await Saved(id, draft, cancellationToken);

        return new VersionedSaveResult.Saved(id);
    }
}
