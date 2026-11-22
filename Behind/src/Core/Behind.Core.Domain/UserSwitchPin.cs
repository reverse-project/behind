using Behind.Common;

namespace Behind.Core.Domain;

public sealed record UserSwitchPin
{
    public HumanUser User { get; private init; } = null!;

    [Sensible]
    public PinHash PinHash { get; init; } = null!;

    private UserSwitchPin()
    {
    }

    public UserSwitchPin(HumanUser user, PinHash pinHash)
    {
        User = user;
        PinHash = pinHash;
    }
}
