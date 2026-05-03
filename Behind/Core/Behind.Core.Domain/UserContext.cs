namespace Behind.Core.Domain;

public abstract record UserContext(User User);

public sealed record SelfUserContext(User User) : UserContext(User);

public sealed record OnBehalfOfUserContext(User Impersonator, User BehalfOf) : UserContext(BehalfOf);