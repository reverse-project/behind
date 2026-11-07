using Behind.Common;

namespace Behind.Core.Domain;

public sealed record AccessSettings(
    VersionedKey<Guid> Id,
    bool RestrictionEnabled,
    string RestrictedToRole,
    string RestrictionMessage,
    bool ModeratedSignUp,
    string RestrictionTitle = "")
{
    public const string EntityType = nameof(AccessSettings);
    public const int MaxRoleLength = 100;
    public const int MaxTitleLength = 200;
    public const int MaxMessageLength = 20_000;

    public static readonly Guid SingletonKey = Guid.Parse("80000000-0000-0000-0000-000000000001");

    public AccessSettings(
        bool restrictionEnabled,
        string restrictedToRole,
        string restrictionMessage,
        bool moderatedSignUp,
        string restrictionTitle = "")
        : this(
            new VersionedKey<Guid>(Guid.Empty),
            restrictionEnabled,
            restrictedToRole,
            restrictionMessage,
            moderatedSignUp,
            restrictionTitle)
    {
    }

    public static AccessSettings Open { get; } = new(false, DomainConstants.UsersRole, string.Empty, false);
}
