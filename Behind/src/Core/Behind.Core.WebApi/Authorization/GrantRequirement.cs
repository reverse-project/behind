using Behind.Common;
using Behind.Common.Logging;
using Behind.Core.Database;
using Behind.Core.WebApi.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi.Authorization;

public sealed record GrantRequirement(string Grant) : IAuthorizationRequirement;

public sealed class GrantAuthorizationHandler(IUserQueries userQueries, ILogger<GrantAuthorizationHandler> logger)
    : AuthorizationHandler<GrantRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        GrantRequirement requirement)
    {
        var granted = await context.User.Email.Match(
            () => Task.FromResult(false),
            email => userQueries.HasGrant(email, requirement.Grant));

        if (granted)
        {
            logger.LogDebug("Grant {Grant} satisfied", () => [requirement.Grant]);

            context.Succeed(requirement);
            return;
        }

        logger.LogWarning("Grant {Grant} is not held by the current user", () => [requirement.Grant]);
    }
}
