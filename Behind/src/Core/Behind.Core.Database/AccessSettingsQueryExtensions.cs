using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

internal sealed record AccessSettingsRow(
    Guid Key,
    long Version,
    bool RestrictionEnabled,
    string RestrictedToRole,
    string RestrictionMessage,
    bool ModeratedSignUp,
    string RestrictionTitle);

internal static class AccessSettingsQueryExtensions
{
    extension(IQueryable<AccessSettings> settings)
    {
        public IQueryable<AccessSettingsRow> ToRows() =>
            settings
                .OrderByDescending(item => EF.Property<long>(item, VersionedColumns.Version))
                .Select(item => new AccessSettingsRow(
                    EF.Property<Guid>(item, VersionedColumns.Key),
                    EF.Property<long>(item, VersionedColumns.Version),
                    item.RestrictionEnabled,
                    item.RestrictedToRole,
                    item.RestrictionMessage,
                    item.ModeratedSignUp,
                    item.RestrictionTitle));
    }

    extension(AccessSettingsRow row)
    {
        public AccessSettings ToSettings() =>
            new(
                new VersionedKey<Guid>(row.Key, row.Version),
                row.RestrictionEnabled,
                row.RestrictedToRole,
                row.RestrictionMessage,
                row.ModeratedSignUp,
                row.RestrictionTitle);
    }

    extension(AbstractCoreContext context)
    {
        public IQueryable<AccessSettings> LatestAccessSettingsIn(DeletionState state) =>
            context.AccessSettings
                .OfKey(AccessSettings.SingletonKey)
                .InDeletionState(context, AbstractCoreContext.DeletionRecorded, state)
                .LatestOf(context.AccessSettings);
    }
}
