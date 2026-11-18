using Behind.Common;
using Behind.Common.Logging;
using Behind.Core.Database;
using Behind.Core.WebApi.Middleware;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi.Authorization;

public sealed record GrantRequirement(string Grant) : IAuthorizationRequirement;

public sealed class GrantAuthorizationHandler(
    ISessionQueries sessions,
    IAccessQueries accessQueries,
    ILogger<GrantAuthorizationHandler> logger,
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<GrantRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        GrantRequirement requirement)
    {
        var httpContext = context.Resource as HttpContext ?? httpContextAccessor.HttpContext;
        var granted = false;

        if (httpContext is not null
            && ReferenceEquals(context.User, httpContext.User)
            && context.User.Identity?.IsAuthenticated == true)
        {
            var session = await httpContext.FindCurrentSession(sessions);
            granted = await session.Match(
                () => Task.FromResult(false),
                async current => (await accessQueries.GetUserAccess(current.UserId, httpContext.RequestAborted)).Holds(requirement.Grant));
        }

        if (granted)
        {
            logger.LogDebug("Grant {Grant} satisfied", () => [requirement.Grant]);

            context.Succeed(requirement);
            return;
        }

        logger.LogWarning("Grant {Grant} is not held by the current user", () => [requirement.Grant]);
    }
}
