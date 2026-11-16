using Behind.Common.Database;

namespace Behind.Core.Database;

public interface IAccessSettingsCommands
{
    public sealed record AccessSettingsDraft(
        bool RestrictionEnabled,
        string RestrictedToRole,
        string RestrictionMessage,
        bool ModeratedSignUp,
        string RestrictionTitle = "");

    Task<AccessSettingsSaveResult> Save(AccessSettingsDraft draft, CancellationToken cancellationToken = default);

    Task<bool> Delete(CancellationToken cancellationToken = default);

    Task<AccessSettingsSaveResult> Restore(CancellationToken cancellationToken = default);

    Task<HardDeleteResult> HardDelete(CancellationToken cancellationToken = default);
}
