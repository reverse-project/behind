using Behind.Common;

namespace Behind.Core.Domain;

public sealed record UserRecoveryCode([property: Sensible] string Hash)
{
    public const int MaxHashLength = 100;
}
