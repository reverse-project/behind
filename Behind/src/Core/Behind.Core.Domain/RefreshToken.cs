using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Domain;

public sealed record RefreshToken
{
    [Sensible]
    public TokenNonce Nonce { get; init; }
    public User User { get; init; }
    public DateTimeOffset IssuedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }

    private RefreshToken() =>
        (Nonce, User, IssuedAt, ExpiresAt) = (Guid.Empty, new GuestUser(None), default, default);

    private RefreshToken(Guid nonce, User user, DateTimeOffset issuedAt, TimeSpan duration) =>
        (Nonce, User, IssuedAt, ExpiresAt) = (nonce, user, issuedAt, issuedAt.Add(duration));

    public static RefreshToken Create(User user) =>
        Create(user, TimeProvider.System.GetUtcNow(), DomainConstants.RefreshTokenDuration);

    public static RefreshToken Create(User user, TimeSpan duration) =>
        Create(user, TimeProvider.System.GetUtcNow(), duration);

    public static RefreshToken Create(User user, DateTimeOffset issuedAt, TimeSpan duration) =>
        new(Guid.NewGuid(), user, issuedAt, duration);

    public RefreshToken Rotate() => this with { Nonce = Guid.NewGuid() };

    public static RefreshToken CreateExisting(Guid nonce, User user, DateTimeOffset issuedAt, TimeSpan duration) =>
        new(nonce, user, issuedAt, duration);
}