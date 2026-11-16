using Behind.Core.Common.Configuration;
using Behind.Core.Domain;
using Behind.Core.WebApi.Identity;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public static class SignInResponseExtensions
{
    extension(ApplicationUser user)
    {
        public bool HasConfirmedEmail => user.Emails.Any(email => email.ConfirmedAt is not null);

        public HumanUser ToHumanUser() => new(user.DisplayName, user.Emails, None);

        public SignInEndpoints.SignInResponse ToSignInResponse(
            AccessToken accessToken,
            RefreshToken refreshToken,
            Guid sessionId,
            JwtSettings jwtSettings) =>
            new(
                accessToken.ToJwtToken(user, sessionId, jwtSettings),
                accessToken.ExpiresAt,
                refreshToken.ToJwtToken(user, sessionId, jwtSettings));
    }
}
