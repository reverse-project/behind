namespace Behind.Core.Domain;

public sealed record AccessSelection(IReadOnlyList<string> Grants, IReadOnlyList<string> Restrictions)
{
    public static readonly AccessSelection Empty = new([], []);
}
