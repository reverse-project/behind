using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Behind.Tests.Helpers;

public sealed class FakeAuthorizationService(params string[] grants) : IAuthorizationService
{
    public Task<AuthorizationResult> AuthorizeAsync(
        ClaimsPrincipal user,
        object? resource,
        IEnumerable<IAuthorizationRequirement> requirements) =>
        Task.FromResult(AuthorizationResult.Failed());

    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName) =>
        Task.FromResult(grants.Contains(policyName) ? AuthorizationResult.Success() : AuthorizationResult.Failed());
}
