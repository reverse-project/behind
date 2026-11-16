using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Domain;

public abstract record Role(string Name, Option<IEnumerable<Grant>> Grants)
{
    public Option<IEnumerable<Restriction>> Restrictions { get; init; } = None;
}

public sealed record UserRole(string Name, Option<IEnumerable<Grant>> Grants) : Role(Name, Grants)
{
    public bool IsDeveloper { get; init; }

    public UserRole(string name) : this(name, None)
    {
        
    }
}

public sealed record SystemRole(Option<IEnumerable<Grant>> Grants) : Role(DomainConstants.System, Grants);