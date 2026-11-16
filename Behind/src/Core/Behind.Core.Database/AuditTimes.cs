using Behind.Common;

namespace Behind.Core.Database;

public sealed record AuditTimes(Option<DateTimeOffset> CreatedAt, Option<DateTimeOffset> UpdatedAt);
