namespace Behind.Core.Domain;

public sealed record Restriction(string Name)
{
    public static implicit operator string(Restriction restriction) => restriction.Name;
    public static implicit operator Restriction(string name) => new(name);

    public override string ToString() => Name;
}
