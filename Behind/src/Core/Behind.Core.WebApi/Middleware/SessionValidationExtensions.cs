using System.Security.Claims;
using static Behind.Common.FunctionalExtensions;
using Behind.Common;
using Behind.Core.Domain;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Middleware;

public static class SessionValidationExtensions
{
    extension(ClaimsPrincipal principal)
    {
        public Option<SessionClaims> SessionClaims =>
            from sessionId in principal.GuidClaim(SessionClaimTypes.SessionId)
            from nonce in principal.GuidClaim(SessionClaimTypes.TokenNonce)
            select new SessionClaims(sessionId, nonce);

        private Option<Guid> GuidClaim(string type) =>
            Guid.TryParse(principal.FindFirstValue(type), out var value) ? Some(value) : None;
    }

    extension(Session session)
    {
        public Option<SessionRejection> RejectionFor(TokenNonce nonce, DateTimeOffset now) =>
            session switch
            {
                ValidSession valid => valid.RejectionFor(nonce, now),
                _ => Some(SessionRejection.Invalidated)
            };
    }

    extension(ValidSession session)
    {
        private Option<SessionRejection> RejectionFor(TokenNonce nonce, DateTimeOffset now) =>
            session.AccessToken.Nonce != nonce
                ? Some(SessionRejection.TokenMismatch)
                : session.AccessToken.ExpiresAt <= now || session.RefreshToken.ExpiresAt <= now
                    ? Some(SessionRejection.Expired)
                    : None;
    }

    extension(HttpContext context)
    {
        public Task Reject(SessionRejection rejection)
        {
            context.Response.StatusCode = rejection.StatusCode;
            context.Response.Headers.WWWAuthenticate =
                $"Bearer error=\"invalid_token\", error_description=\"{rejection.Reason}\"";
            return Task.CompletedTask;
        }
    }

    extension(IApplicationBuilder app)
    {
        public IApplicationBuilder UseSessionValidation() => app.UseMiddleware<SessionValidationMiddleware>();
    }
}
