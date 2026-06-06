namespace Behind.Core.Domain;

public sealed record Password(string Value, bool IsActive = false);
