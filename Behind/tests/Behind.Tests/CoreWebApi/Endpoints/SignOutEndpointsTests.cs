using System.Security.Claims;
using Behind.Common;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Endpoints;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class SignOutEndpointsTests
{
    private static DefaultHttpContext NewHttpContextWithAuth(Guid sessionId)
    {
        var context = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new Claim("session_id", sessionId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        context.User = principal;
        return context;
    }

    private static DefaultHttpContext NewHttpContextWithoutAuth()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal();
        return context;
    }

    private static ISessionCommands MockSessionCommands() => Substitute.For<ISessionCommands>();

    private static ISessionQueries MockSessionQueriesFor(
        Guid sessionId, 
        Guid userId, 
        bool isValid = true) =>
        MockSessionQueriesFor(sessionId, Some<(Session, Guid)>(
            (isValid 
                ? (Session)new ValidSession(
                    AccessToken.Create(new GuestUser(None)),
                    RefreshToken.Create(new GuestUser(None)),
                    "10.0.0.1",
                    "test-agent")
                : new InvalidSession(Some<User>(new HumanUser("User", [new Email("user@test.com")], None)), Some(TimeProvider.System.GetUtcNow())), 
            userId)));

    private static ISessionQueries MockSessionQueriesFor(
        Guid sessionId,
        Option<(Session, Guid)> result)
    {
        var queries = Substitute.For<ISessionQueries>();
        queries.TryFindByIdWithUserId(sessionId, Arg.Any<CancellationToken>()).Returns(result);
        return queries;
    }

    [Fact]
    public async Task SignOut_ValidAuth_LogsInformationWithOperationSessionAndUser()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var logger = new CapturingLogger<SignOutEndpoints>();

        await SignOutEndpoints.SignOut(
            NewHttpContextWithAuth(sessionId),
            MockSessionCommands(),
            MockSessionQueriesFor(sessionId, userId),
            TimeProvider.System,
            logger);

        var entry = logger.Single(LogLevel.Information);
        Assert.Equal(nameof(SignOutEndpoints.SignOut), entry.State["Operation"]);
        Assert.Equal(sessionId, entry.State["SessionId"]);
        Assert.Equal(userId, entry.State["UserId"]);
    }

    [Fact]
    public async Task SignOutAll_ValidAuth_LogsInformationWithOperation()
    {
        var sessionId = Guid.NewGuid();
        var logger = new CapturingLogger<SignOutEndpoints>();

        await SignOutEndpoints.SignOutAll(
            NewHttpContextWithAuth(sessionId),
            MockSessionCommands(),
            MockSessionQueriesFor(sessionId, Guid.NewGuid()),
            TimeProvider.System,
            logger);

        Assert.Equal(nameof(SignOutEndpoints.SignOutAll), logger.Single(LogLevel.Information).State["Operation"]);
    }

    [Fact]
    public async Task SignOut_WithoutSession_LogsWarning()
    {
        var logger = new CapturingLogger<SignOutEndpoints>();

        await SignOutEndpoints.SignOut(
            NewHttpContextWithoutAuth(),
            MockSessionCommands(),
            Substitute.For<ISessionQueries>(),
            TimeProvider.System,
            logger);

        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public async Task SignOut_ValidAuth_InvalidatesCurrentSession()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var context = NewHttpContextWithAuth(sessionId);
        var commands = MockSessionCommands();
        var queries = MockSessionQueriesFor(sessionId, userId);

        var result = await SignOutEndpoints.SignOut(
            context, commands, queries, TimeProvider.System, NullLogger<SignOutEndpoints>.Instance);

        var noContent = Assert.IsType<NoContent>(result);
        await commands.Received(1).InvalidateSession(
            sessionId,
            userId,
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SignOut_WithoutSessionId_ReturnsUnauthorized()
    {
        var context = NewHttpContextWithoutAuth();
        var commands = MockSessionCommands();
        var queries = Substitute.For<ISessionQueries>();

        var result = await SignOutEndpoints.SignOut(
            context, commands, queries, TimeProvider.System, NullLogger<SignOutEndpoints>.Instance);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task SignOut_SessionNotFound_ReturnsUnauthorized()
    {
        var sessionId = Guid.NewGuid();
        var context = NewHttpContextWithAuth(sessionId);
        var commands = MockSessionCommands();
        var queries = MockSessionQueriesFor(sessionId, None);

        var result = await SignOutEndpoints.SignOut(
            context, commands, queries, TimeProvider.System, NullLogger<SignOutEndpoints>.Instance);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task SignOut_InvalidSession_ReturnsUnauthorized()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var context = NewHttpContextWithAuth(sessionId);
        var commands = MockSessionCommands();
        var queries = MockSessionQueriesFor(sessionId, userId, isValid: false);

        var result = await SignOutEndpoints.SignOut(
            context, commands, queries, TimeProvider.System, NullLogger<SignOutEndpoints>.Instance);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task SignOutAll_ValidAuth_InvalidatesAllSessions()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var context = NewHttpContextWithAuth(sessionId);
        var commands = MockSessionCommands();
        var queries = MockSessionQueriesFor(sessionId, userId);

        var result = await SignOutEndpoints.SignOutAll(
            context, commands, queries, TimeProvider.System, NullLogger<SignOutEndpoints>.Instance);

        var noContent = Assert.IsType<NoContent>(result);
        await commands.Received(1).InvalidateAllSessions(
            userId,
            userId,
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SignOutAll_WithoutSessionId_ReturnsUnauthorized()
    {
        var context = NewHttpContextWithoutAuth();
        var commands = MockSessionCommands();
        var queries = Substitute.For<ISessionQueries>();

        var result = await SignOutEndpoints.SignOutAll(
            context, commands, queries, TimeProvider.System, NullLogger<SignOutEndpoints>.Instance);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task SignOutAll_SessionNotFound_ReturnsUnauthorized()
    {
        var sessionId = Guid.NewGuid();
        var context = NewHttpContextWithAuth(sessionId);
        var commands = MockSessionCommands();
        var queries = MockSessionQueriesFor(sessionId, None);

        var result = await SignOutEndpoints.SignOutAll(
            context, commands, queries, TimeProvider.System, NullLogger<SignOutEndpoints>.Instance);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task SignOut_PassesCorrectionCancellationToken()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        var context = NewHttpContextWithAuth(sessionId);
        context.RequestAborted = cts.Token;
        var commands = MockSessionCommands();
        var queries = MockSessionQueriesFor(sessionId, userId);

        await SignOutEndpoints.SignOut(
            context, commands, queries, TimeProvider.System, NullLogger<SignOutEndpoints>.Instance);

        await commands.Received(1).InvalidateSession(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<DateTimeOffset>(),
            cts.Token);
    }
}
