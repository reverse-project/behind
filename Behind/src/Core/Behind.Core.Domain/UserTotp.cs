using Behind.Common;

namespace Behind.Core.Domain;

public sealed record UserTotp([property: Sensible] string Secret, bool IsConfirmed = false, long LastUsedStep = 0)
{
    public const int MaxSecretLength = 128;
}
