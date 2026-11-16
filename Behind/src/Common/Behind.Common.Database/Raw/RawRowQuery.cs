namespace Behind.Common.Database.Raw;

public sealed record RawRowQuery(Flipper Flipper, Option<string> Search, Option<string> OrderBy, bool Descending);
