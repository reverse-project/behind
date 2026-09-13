namespace Behind.Core.Domain;

public abstract record SystemContext;

public sealed record AutonomousSystemContext : SystemContext;

public sealed record UserDrivenSystemContext(User User) : SystemContext;
