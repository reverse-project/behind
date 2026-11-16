using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Domain;

public sealed record MagicLink(
    TokenNonce Nonce,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    MagicLinkFunction Function = MagicLinkFunction.SignIn,
    Option<DateTimeOffset> ConsumedAt = default
)
{
    private MagicLink() : this(Guid.Empty, default, default)
    {
    }

    public static MagicLink Create(
        DateTimeOffset issuedAt,
        TimeSpan lifetime,
        MagicLinkFunction function = MagicLinkFunction.SignIn) =>
        new(Guid.NewGuid(), issuedAt, issuedAt.Add(lifetime), function, None);

    public bool IsUsableAt(DateTimeOffset moment) => ConsumedAt == None && moment < ExpiresAt;
}
