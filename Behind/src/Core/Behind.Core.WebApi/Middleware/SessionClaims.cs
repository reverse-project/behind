using Behind.Core.Domain;

namespace Behind.Core.WebApi.Middleware;

public sealed record SessionClaims(Guid SessionId, TokenNonce Nonce);
