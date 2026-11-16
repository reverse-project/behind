namespace Behind.Common.Database;

public sealed record ActiveVersion(string EntityType, string EntityKey, long Version = 0);
