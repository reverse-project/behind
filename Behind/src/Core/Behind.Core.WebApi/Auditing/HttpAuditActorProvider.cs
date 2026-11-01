using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.WebApi.Middleware;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Auditing;

public sealed class HttpAuditActorProvider(
    IHttpContextAccessor httpContextAccessor,
    ISessionQueries sessionQueries
) : IAuditActorProvider
{
    public async Task<AuditActor> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (httpContextAccessor.HttpContext is not { } httpContext)
        {
            return AuditActor.Autonomous;
        }

        return await httpContext.User.SessionId.Match(
            none: () => Task.FromResult(AuditActor.Anonymous),
            some: async sessionId =>
            {
                var session = await sessionQueries.TryFindActor(sessionId, cancellationToken);

                return session.Match(
                    none: () => AuditActor.Anonymous,
                    some: found => AuditActor.ForSession(found.UserId, found.ImpersonatorId)
                );
            }
        );
    }
}
