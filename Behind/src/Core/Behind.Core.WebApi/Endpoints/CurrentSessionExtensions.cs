using Behind.Common;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed record CurrentSession(Guid SessionId, Guid UserId);

public static class CurrentSessionExtensions
{
    extension(HttpContext httpContext)
    {
        public async Task<Option<CurrentSession>> FindCurrentSession(ISessionQueries sessionQueries) =>
            await httpContext.User.SessionId.Match(
                () => Task.FromResult<Option<CurrentSession>>(None),
                async sessionId =>
                {
                    var found = await sessionQueries.TryFindByIdWithUserId(sessionId, httpContext.RequestAborted);

                    return found
                        .Where(result => result.Session is ValidSession)
                        .Map(result => new CurrentSession(sessionId, result.UserId));
                });
    }
}
