using Behind.Common;
using Behind.Core.Domain;

namespace Behind.Core.WebApi.Endpoints;

public static class SessionResponseExtensions
{
    extension(Role role)
    {
        public SessionRole ToSessionRole() =>
            new(role.Name, role.EffectiveGrants.Names);
    }

    extension(User user)
    {
        public SessionImpersonator? ToSessionImpersonator() =>
            user is HumanUser human
                ? new SessionImpersonator(human.DisplayName, human.Emails.Select(email => email.Value).ToList())
                : null;
    }

    extension(ValidSession session)
    {
        public SessionDetails ToSessionDetails(DateTimeOffset now) =>
            new(
                session.RefreshToken.IssuedAt,
                now - session.RefreshToken.IssuedAt,
                session.AccessToken.IssuedAt,
                session.AccessToken.ExpiresAt,
                session.AccessToken.ExpiresAt - now,
                session.RefreshToken.ExpiresAt,
                session.RefreshToken.ExpiresAt - now,
                session.RemoteAddress,
                session.UserAgent);

        public SessionResponse ToSessionResponse(
            Guid sessionId,
            Guid userId,
            HumanUser user,
            UserAccess access,
            DateTimeOffset now,
            RefreshedTokens? refreshed) =>
            new(
                sessionId,
                new SessionUser(
                    userId,
                    user.DisplayName,
                    user.Emails.Select(email => email.Value).ToList(),
                    access.Roles.Select(role => role.ToSessionRole()).ToList(),
                    access.CalculatedGrants.Names),
                session.ToSessionDetails(now),
                refreshed,
                session.Impersonator.Match<SessionImpersonator?>(() => null, impersonator => impersonator.ToSessionImpersonator()));
    }
}
