namespace Behind.Core.Domain;

public sealed record Email(string Value, DateTimeOffset? ConfirmedAt = null);