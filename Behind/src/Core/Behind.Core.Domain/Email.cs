namespace Behind.Core.Domain;

public sealed record Email(string Value, bool IsConfirmed = false);