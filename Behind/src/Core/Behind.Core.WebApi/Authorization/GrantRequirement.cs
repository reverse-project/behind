using Behind.Common;
using Behind.Core.Database;
using Behind.Core.WebApi.Middleware;
using Microsoft.AspNetCore.Authorization;

namespace Behind.Core.WebApi.Authorization;

public sealed record GrantRequirement(string Grant) : IAuthorizationRequirement;

public sealed class GrantAuthorizationHandler(IUserQueries userQueries) : AuthorizationHandler<GrantRequirement>
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
            context.Succeed(requirement);
        }
    }
}
