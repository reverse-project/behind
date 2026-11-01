using System.Security.Claims;
using Behind.Core.Database;
using Behind.Core.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Behind.Tests.CoreWebApi.Authorization;

public class GrantAuthorizationHandlerTests
{
    private static readonly GrantRequirement Requirement = new("AuditLog.Read");

    private static AuthorizationHandlerContext ContextFor(ClaimsPrincipal user) =>
        new([Requirement], user, null);

    private static ClaimsPrincipal SignedIn(string email) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Email, email)], "test"));

    private static IUserQueries QueriesGranting(string email, bool granted)
    {
        var queries = Substitute.For<IUserQueries>();
        queries.HasGrant(email, Requirement.Grant, Arg.Any<CancellationToken>()).Returns(granted);
        return queries;
    }

    [Fact]
    public async Task Handle_UserHasGrant_Succeeds()
    {
        var context = ContextFor(SignedIn("jane@test.com"));

        await new GrantAuthorizationHandler(QueriesGranting("jane@test.com", true)).HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Handle_UserLacksGrant_DoesNotSucceed()
    {
        var context = ContextFor(SignedIn("jane@test.com"));

        await new GrantAuthorizationHandler(QueriesGranting("jane@test.com", false)).HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Handle_Anonymous_DoesNotSucceedAndSkipsLookup()
    {
        var queries = Substitute.For<IUserQueries>();
        var context = ContextFor(new ClaimsPrincipal(new ClaimsIdentity()));

        await new GrantAuthorizationHandler(queries).HandleAsync(context);

        Assert.False(context.HasSucceeded);
        await queries.DidNotReceiveWithAnyArgs().HasGrant(default!, default!, default);
    }
}
