namespace Behind.Core.Domain;

public sealed record MagicLinkRedemption(Guid UserId, MagicLinkFunction Function);
