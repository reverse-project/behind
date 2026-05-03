namespace Behind.Core.Domain;

public sealed record ActiveVersion(string EntityType, string EntityKey, long Version = 0);