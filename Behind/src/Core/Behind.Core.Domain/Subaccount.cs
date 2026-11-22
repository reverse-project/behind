using Behind.Common;

namespace Behind.Core.Domain;

public sealed record Subaccount(Guid PrimaryUserId, Guid UserId, bool IsChild, [property: Sensible] string? PinHash);
