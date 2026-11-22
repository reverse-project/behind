using Behind.Core.Common.Configuration;
using Behind.Core.Domain;
using Behind.Core.WebApi.Identity;

namespace Behind.Core.WebApi.Endpoints;

public static class SignInResponseExtensions
{
    extension(ApplicationUser user)
    {
        public bool HasConfirmedEmail => user.Emails.Any(email => email.ConfirmedAt is not null);

        public HumanUser ToHumanUser() => new() { DisplayName = user.DisplayName, Emails = user.Emails.ToList(), PersistedId = user.Id };

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
