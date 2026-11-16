using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public sealed class AccessSettingsCommands(AbstractCoreContext context, IVfsCommands files) : IAccessSettingsCommands
{
    public async Task<AccessSettingsSaveResult> Save(
        IAccessSettingsCommands.AccessSettingsDraft draft,
        CancellationToken cancellationToken = default)
    {
        if (!await context.Roles.AnyAsync(role => role.Name == draft.RestrictedToRole, cancellationToken))
        {
            return new AccessSettingsSaveResult.UnknownRole();
        }

        var id = new VersionedKey<Guid>(
            AccessSettings.SingletonKey,
            await LatestVersion(cancellationToken) + 1);

        var settings = new AccessSettings(
            draft.RestrictionEnabled,
            draft.RestrictedToRole,
            draft.RestrictionMessage,
            draft.ModeratedSignUp,
            draft.RestrictionTitle);

        await context.AccessSettings.AddAsync(settings, cancellationToken);
        context.Entry(settings).StampVersionedKey(id.Key, id.Version);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();

            return new AccessSettingsSaveResult.VersionConflict();
        }

        await files.AttachSiteImagesOf(
            VfsTypes.AccessSettings,
            AccessSettings.SingletonKey,
            [draft.RestrictionMessage],
            cancellationToken);

        return new AccessSettingsSaveResult.Saved(id);
    }

    public async Task<bool> Delete(CancellationToken cancellationToken = default)
    {
        var deleted = await context.MarkSoftDeletedAsync(Rows, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return deleted > 0;
    }

    public async Task<AccessSettingsSaveResult> Restore(CancellationToken cancellationToken = default)
    {
        if (await ActiveRowsExist(cancellationToken) || !await context.HasDeletedRowsAsync(Rows, cancellationToken))
        {
            return new AccessSettingsSaveResult.NotFound();
        }

        var version = await LatestVersion(cancellationToken);

        await context.MarkRestoredAsync(Rows, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new AccessSettingsSaveResult.Saved(new VersionedKey<Guid>(AccessSettings.SingletonKey, version));
    }

    public async Task<HardDeleteResult> HardDelete(CancellationToken cancellationToken = default)
    {
        var deleted = await context.MarkHardDeletedAsync(Rows, cancellationToken);

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

    private IQueryable<AccessSettings> Rows => context.AccessSettings.OfKey(AccessSettings.SingletonKey);

    private async Task<bool> ActiveRowsExist(CancellationToken cancellationToken) =>
        await Rows.AnyAsync(cancellationToken);

    private async Task<long> LatestVersion(CancellationToken cancellationToken)
    {
        using var scope = context.SoftDelete.IncludeDeleted();

        return await Rows.LatestVersionAsync(AccessSettings.SingletonKey, cancellationToken);
    }
}
