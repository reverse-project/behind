using System.Security.Claims;
using Behind.Core.Database;
using Behind.Core.WebApi.Authorization;
using Behind.Tests.Helpers;
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
    public async Task Handle_UserHasGrant_LogsDebugWithGrant()
    {
        var logger = new CapturingLogger<GrantAuthorizationHandler>();

        await new GrantAuthorizationHandler(QueriesGranting("jane@test.com", true), logger)
            .HandleAsync(ContextFor(SignedIn("jane@test.com")));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Equal(Requirement.Grant, entry.State["Grant"]);
    }

    [Fact]
    public async Task Handle_UserLacksGrant_LogsWarningWithGrant()
    {
        var logger = new CapturingLogger<GrantAuthorizationHandler>();

        await new GrantAuthorizationHandler(QueriesGranting("jane@test.com", false), logger)
            .HandleAsync(ContextFor(SignedIn("jane@test.com")));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(Requirement.Grant, entry.State["Grant"]);
    }

    [Fact]
    public async Task Handle_UserHasGrant_Succeeds()
    {
        var context = ContextFor(SignedIn("jane@test.com"));

        await new GrantAuthorizationHandler(QueriesGranting("jane@test.com", true), NullLogger<GrantAuthorizationHandler>.Instance).HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Handle_UserLacksGrant_DoesNotSucceed()
    {
        var context = ContextFor(SignedIn("jane@test.com"));

        await new GrantAuthorizationHandler(QueriesGranting("jane@test.com", false), NullLogger<GrantAuthorizationHandler>.Instance).HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Handle_Anonymous_DoesNotSucceedAndSkipsLookup()
    {
        var queries = Substitute.For<IUserQueries>();
        var context = ContextFor(new ClaimsPrincipal(new ClaimsIdentity()));

        await new GrantAuthorizationHandler(queries, NullLogger<GrantAuthorizationHandler>.Instance).HandleAsync(context);

        Assert.False(context.HasSucceeded);
        await queries.DidNotReceiveWithAnyArgs().HasGrant(default!, default!, default);
    }
}
