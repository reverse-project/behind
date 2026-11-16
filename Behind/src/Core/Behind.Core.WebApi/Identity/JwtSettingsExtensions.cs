using System.Security.Claims;
using System.Text;
using Behind.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Middleware;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Identity;

public static class JwtSettingsExtensions
{
    public static TokenValidationParameters RejectingValidationParameters =>
        new()
        {
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true
        };

    extension(JwtSettings settings)
    {
        public TokenValidationParameters ValidationParameters => new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = settings.Issuer,
            ValidAudience = settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
            ClockSkew = TimeSpan.Zero
        };

        public async Task<Option<SessionClaims>> ReadSessionClaims(string token)
        {
            var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, settings.ValidationParameters);

            return result.IsValid
                ? new ClaimsPrincipal(result.ClaimsIdentity).SessionClaims
                : None;
        }
    }
}
