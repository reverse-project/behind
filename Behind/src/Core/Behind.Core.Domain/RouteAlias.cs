using Behind.Common;

namespace Behind.Core.Domain;

public sealed record RouteAlias(VersionedKey<Guid> Id, string Alias, string Target)
{
    public const string EntityType = nameof(RouteAlias);
    public const int MaxAliasLength = 100;
    public const int MaxTargetLength = 2_000;

    public RouteAlias(string alias, string target) : this(new VersionedKey<Guid>(Guid.Empty), alias, target)
    {
    }
}
