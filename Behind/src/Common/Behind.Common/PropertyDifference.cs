namespace Behind.Common;

public sealed record PropertyDifference(
    string PropertyName,
    object? OldValue,
    object? NewValue
);