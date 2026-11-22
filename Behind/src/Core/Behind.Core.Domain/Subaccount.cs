namespace Behind.Core.Domain;

public sealed record Subaccount
{
    public HumanUser PrimaryUser { get; private init; } = null!;

    public HumanUser User { get; private init; } = null!;

    public bool IsChild { get; init; }

    private Subaccount()
    {
    }

    public Subaccount(HumanUser primaryUser, HumanUser user, bool isChild)
    {
        PrimaryUser = primaryUser;
        User = user;
        IsChild = isChild;
    }
}
