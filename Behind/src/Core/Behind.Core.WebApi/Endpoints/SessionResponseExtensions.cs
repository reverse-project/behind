using Behind.Common;
using Behind.Core.Domain;

namespace Behind.Core.WebApi.Endpoints;

public static class SessionResponseExtensions
{
    extension(Role role)
    {
        public SessionRole ToSessionRole() =>
            new(
                role.Name,
                role.Grants.Match(
                    () => (IReadOnlyList<string>)[],
                    grants => grants.Select(grant => grant.Name).ToList()));
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
            IEnumerable<Role> roles,
            DateTimeOffset now,
            RefreshedAccessToken? refreshed) =>
            new(
                sessionId,
                new SessionUser(
                    userId,
                    user.DisplayName,
                    user.Emails.Select(email => email.Value).ToList(),
                    roles.Select(role => role.ToSessionRole()).ToList()),
                session.ToSessionDetails(now),
                refreshed);
    }
}
