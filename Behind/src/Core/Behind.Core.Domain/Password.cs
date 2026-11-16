using Behind.Common;

namespace Behind.Core.Domain;

public sealed record Password([property: Sensible] string Value, bool IsActive = false);
