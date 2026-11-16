namespace Behind.Core.Domain;

public sealed record Grant(string Name)
{
    public static implicit operator string(Grant grant) => grant.Name;
    public static implicit operator Grant(string name) => new(name);

    public override string ToString() => Name;
}
