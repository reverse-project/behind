using System.Security.Claims;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Identity;

namespace Behind.Core.WebApi.Mcp;

public sealed record ServiceUserIdentity(ServiceUserEntry ServiceUser, Guid SessionId, ValidSession Session)
{
    public static ServiceUserIdentity Create(ServiceUserEntry user, DateTimeOffset now)
    {
        return new(user, Guid.NewGuid(), new ValidSession(
            user.User,
            AccessToken.Create(user.User, now, DomainConstants.AccessTokenDuration),
            RefreshToken.Create(user.User, now, DomainConstants.RefreshTokenDuration),
            new RemoteAddress("127.0.0.1"),
            new UserAgent("Behind MCP")));
    }

    public ClaimsPrincipal Principal => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, ServiceUser.Id.ToString()),
        new Claim(ClaimTypes.Name, ServiceUser.User.Name),
        new Claim("service_user_id", ServiceUser.Id.ToString()),
        new Claim(SessionClaimTypes.SessionId, SessionId.ToString()),
        new Claim(SessionClaimTypes.TokenNonce, Session.AccessToken.Nonce.ToString())
    ], "ServiceUser"));
}
