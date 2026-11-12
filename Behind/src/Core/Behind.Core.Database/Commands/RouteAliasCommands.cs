using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public sealed class RouteAliasCommands(AbstractCoreContext context) : IRouteAliasCommands
{
    public Task<RouteAliasSaveResult> Create(
        IRouteAliasCommands.RouteAliasDraft draft,
        CancellationToken cancellationToken = default) =>
        AddVersion(Guid.CreateVersion7(), 0, draft, cancellationToken);

    public async Task<RouteAliasSaveResult> Update(
        Guid aliasId,
        IRouteAliasCommands.RouteAliasDraft draft,
        CancellationToken cancellationToken = default)
    {
        var latest = (await context.RouteAliases
                .InDeletionState(context, AbstractCoreContext.DeletionRecorded, DeletionState.Active)
                .OfKey(aliasId)
                .OrderByDescending(alias => EF.Property<long>(alias, VersionedColumns.Version))
                .Select(alias => EF.Property<long>(alias, VersionedColumns.Version))
                .Take(1)
                .ToListAsync(cancellationToken))
            .Head();

        return await latest.Match(
            () => Task.FromResult<RouteAliasSaveResult>(new RouteAliasSaveResult.NotFound()),
            version => AddVersion(aliasId, version, draft, cancellationToken));
    }

    public async Task<bool> Delete(Guid aliasId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.MarkSoftDeletedAsync(context.RouteAliases.OfKey(aliasId), cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return deleted > 0;
    }

    public async Task<RouteAliasSaveResult> Restore(Guid aliasId, CancellationToken cancellationToken = default)
    {
        if (!await context.HasDeletedRowsAsync(context.RouteAliases.OfKey(aliasId), cancellationToken))
        {
            return new RouteAliasSaveResult.NotFound();
        }

        var latest = await Latest(aliasId, cancellationToken);

        if (await IsAliasTaken(aliasId, latest.Alias, cancellationToken))
        {
            return new RouteAliasSaveResult.AliasTaken();
        }

        await context.MarkRestoredAsync(context.RouteAliases.OfKey(aliasId), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new RouteAliasSaveResult.Saved(new VersionedKey<Guid>(aliasId, latest.Version));
    }

    public async Task<HardDeleteResult> HardDelete(Guid aliasId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.MarkHardDeletedAsync(context.RouteAliases.OfKey(aliasId), cancellationToken);

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

    private async Task<RouteAliasSaveResult> AddVersion(
        Guid key,
        long previousVersion,
        IRouteAliasCommands.RouteAliasDraft draft,
        CancellationToken cancellationToken)
    {
        if (await IsAliasTaken(key, draft.Alias, cancellationToken))
        {
            return new RouteAliasSaveResult.AliasTaken();
        }

        var id = new VersionedKey<Guid>(key, previousVersion + 1);
        var alias = new RouteAlias(draft.Alias, draft.Target);

        await context.RouteAliases.AddAsync(alias, cancellationToken);
        context.Entry(alias).StampVersionedKey(id.Key, id.Version);

        if (context.ContentLocaleSpecified)
        {
            await context.StageContentLocale(nameof(RouteAlias), key, context.SelectedContentLocale, cancellationToken);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();

            return new RouteAliasSaveResult.VersionConflict();
        }

        return new RouteAliasSaveResult.Saved(id);
    }

    private async Task<(long Version, string Alias)> Latest(Guid aliasId, CancellationToken cancellationToken)
    {
        using var scope = context.SoftDelete.IncludeDeleted();

        var latest = await context.RouteAliases
            .OfKey(aliasId)
            .OrderByDescending(alias => EF.Property<long>(alias, VersionedColumns.Version))
            .Select(alias => new { Version = EF.Property<long>(alias, VersionedColumns.Version), alias.Alias })
            .FirstAsync(cancellationToken);

        return (latest.Version, latest.Alias);
    }

    private async Task<bool> IsAliasTaken(Guid aliasId, string alias, CancellationToken cancellationToken)
    {
        var locale = context.ContentLocaleSpecified
            ? context.SelectedContentLocale
            : await context.FindContentLocale(nameof(RouteAlias), aliasId, cancellationToken);
        return await context.InContentLocale(context.LatestRouteAliasesIn(DeletionState.Active), nameof(RouteAlias), locale)
            .AnyAsync(
                candidate => candidate.Alias == alias && EF.Property<Guid>(candidate, VersionedColumns.Key) != aliasId,
                cancellationToken);
    }
}
