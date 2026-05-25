namespace Behind.Core.Domain;

public sealed record AccessToken
{
    public Guid Nonce { get; init; }
    public User User { get; init; }
    public DateTimeOffset IssuedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }

    private AccessToken(Guid nonce, User user, DateTimeOffset issuedAt, TimeSpan duration) =>
        (Nonce, User, IssuedAt, ExpiresAt) = (nonce, user, issuedAt, issuedAt.Add(duration));

    public static AccessToken Create(User user) =>
        Create(user, TimeProvider.System.GetUtcNow(), DomainConstants.AccessTokenDuration);

    public static AccessToken Create(User user, TimeSpan duration) =>
        Create(user, TimeProvider.System.GetUtcNow(), duration);

    public static AccessToken Create(User user, DateTimeOffset issuedAt, TimeSpan duration) =>
        new(Guid.NewGuid(), user, issuedAt, duration);

    public static AccessToken CreateExisting(Guid nonce, User user, DateTimeOffset issuedAt, TimeSpan duration) =>
        new(nonce, user, issuedAt, duration);
}