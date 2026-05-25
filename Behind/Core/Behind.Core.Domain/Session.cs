namespace Behind.Core.Domain;

public abstract record Session;
public sealed record ValidSession(User User, TokenNonce AccessToken, TokenNonce RefreshToken) : Session;
public sealed record InvalidSession : Session;