using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Middleware;

public sealed class AccessGate(
    IAccessSettingsQueries settingsQueries,
    ISessionQueries sessionQueries,
    IAccessQueries accessQueries)
{
    public async Task<AccessDecision> Decide(HttpContext httpContext)
    {
        var settings = await settingsQueries.Current(httpContext.RequestAborted);

        if (!settings.RestrictionEnabled)
        {
            return new AccessDecision.Open();
        }

        var access = await AccessOf(httpContext);

        return settings.IsRestrictedFor(access)
            ? new AccessDecision.Restricted(settings.RestrictionMessage, settings.RestrictionTitle)
            : new AccessDecision.Open();
    }

    private async Task<UserAccess> AccessOf(HttpContext httpContext)
    {
        var session = await httpContext.FindCurrentSession(sessionQueries);

        return await session.Match(
            () => Task.FromResult(UserAccess.Empty),
            current => accessQueries.GetUserAccess(current.UserId, httpContext.RequestAborted));
    }
}
