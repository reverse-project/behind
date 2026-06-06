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
        public string ToJwtToken(ApplicationUser user, JwtSettings settings)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var keyBytes = Encoding.UTF8.GetBytes(settings.SigningKey);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity([
                    new Claim(ClaimTypes.Email, user.Emails.First().Value)
                ]),
                Expires = accessToken.ExpiresAt.DateTime,
                Issuer = settings.Issuer,
                Audience = settings.Audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(keyBytes),
                    SecurityAlgorithms.HmacSha256Signature
                )
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
    
    extension(RefreshToken refreshToken)
    {
        public string ToJwtToken(ApplicationUser user, JwtSettings settings)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var keyBytes = Encoding.UTF8.GetBytes(settings.SigningKey);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity([
                    new Claim(ClaimTypes.Email, user.Emails.First().Value)
                ]),
                Expires = refreshToken.ExpiresAt.DateTime,
                Issuer = settings.Issuer,
                Audience = settings.Audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(keyBytes),
                    SecurityAlgorithms.HmacSha256Signature
                )
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
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
}