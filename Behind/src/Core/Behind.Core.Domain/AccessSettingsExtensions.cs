namespace Behind.Core.Domain;

public static class AccessSettingsExtensions
{
    public static readonly string ManageGrant = GrantName.Of(GrantArea.Access, GrantAction.Manage);

    extension(AccessSettings settings)
    {
        public bool IsRestrictedFor(UserAccess access) =>
            settings.RestrictionEnabled
            && !access.HasRole(settings.RestrictedToRole)
            && !access.Holds(ManageGrant);
    }
}
