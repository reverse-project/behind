using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Behind.Core.Common.Configuration;
using Behind.Core.Domain;
using Microsoft.IdentityModel.Tokens;

namespace Behind.Core.WebApi.Identity;

public static class IdentityExtensions
{
    extension(AccessToken accessToken)
    {
        public string ToJwtToken(ApplicationUser user, Guid sessionId, JwtSettings settings) =>
            CreateJwtToken(user, sessionId, accessToken.Nonce, accessToken.ExpiresAt, settings);
    }

    extension(RefreshToken refreshToken)
    {
        public string ToJwtToken(ApplicationUser user, Guid sessionId, JwtSettings settings) =>
            CreateJwtToken(user, sessionId, refreshToken.Nonce, refreshToken.ExpiresAt, settings);
    }

    extension(User user)
    {
        public ApplicationUser ToApplicationUser(Guid id) => user switch
        {
            HumanUser human => new ApplicationUser
            {
                Id = id,
                DisplayName = human.DisplayName,
                Emails = human.Emails,
                Password = null
            },
            GuestUser => new ApplicationUser
            {
                Id = DomainConstants.GuestUserId,
                DisplayName = "Guest"
            },
            SystemUser => new ApplicationUser
            {
                Id = DomainConstants.SystemUserId,
                DisplayName = "System"
            },
            _ => throw new ArgumentException($"Invalid user type {user.GetType().Name}")
        };
    }

    extension(Role role)
    {
        public ApplicationRole ToApplicationRole(Guid id) => new()
        {
            Id = id,
            Name = role.Name
        };
    }

    private static string CreateJwtToken(
        ApplicationUser user,
        Guid sessionId,
        TokenNonce nonce,
        DateTimeOffset expiresAt,
        JwtSettings settings)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.Email, user.Emails.First().Value),
                new Claim(SessionClaimTypes.SessionId, sessionId.ToString()),
                new Claim(SessionClaimTypes.TokenNonce, nonce)
            ]),
            Expires = expiresAt.DateTime,
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                SecurityAlgorithms.HmacSha256Signature
            )
        };
        return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
    }
}