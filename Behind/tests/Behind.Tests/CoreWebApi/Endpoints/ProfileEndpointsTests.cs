using System.Reflection;
using System.Security.Claims;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class ProfileEndpointsTests
{
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static DefaultHttpContext SignedIn()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("session_id", SessionId.ToString())],
            "test"));
        return context;
    }

    private static ISessionQueries SessionQueries(bool valid)
    {
        var queries = Substitute.For<ISessionQueries>();
        queries.TryFindByIdWithUserId(SessionId, Arg.Any<CancellationToken>())
            .Returns(valid
                ? Some<(Session, Guid)>((
                    new ValidSession(
                        AccessToken.Create(new GuestUser(None)),
                        RefreshToken.Create(new GuestUser(None)),
                        "10.0.0.1",
                        "test-agent"),
                    UserId))
                : None);
        return queries;
    }

    private static Task<IResult> Change(
        string? displayName,
        IUserCommands commands,
        ISessionQueries? queries = null,
        HttpContext? context = null) =>
        ProfileEndpoints.ChangeDisplayName(
            new ProfileEndpoints.DisplayNameRequest(displayName),
            context ?? SignedIn(),
            queries ?? SessionQueries(true),
            commands);

    [Fact]
    public async Task ChangeDisplayName_ValidSession_NormalizesAndStoresName()
    {
        var commands = Substitute.For<IUserCommands>();
        commands.ChangeDisplayName(UserId, "Ada", Arg.Any<CancellationToken>()).Returns(true);

        var result = await Change("  Ada  ", commands);

        Assert.IsType<NoContent>(result);
        await commands.Received(1).ChangeDisplayName(UserId, "Ada", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ChangeDisplayName_BlankName_StoresEmptyName(string? displayName)
    {
        var commands = Substitute.For<IUserCommands>();
        commands.ChangeDisplayName(UserId, string.Empty, Arg.Any<CancellationToken>()).Returns(true);

        var result = await Change(displayName, commands);

        Assert.IsType<NoContent>(result);
        await commands.Received(1).ChangeDisplayName(UserId, string.Empty, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeDisplayName_WithoutValidSession_ReturnsUnauthorized()
    {
        var commands = Substitute.For<IUserCommands>();

        var result = await Change("Ada", commands, SessionQueries(false));

        Assert.IsType<UnauthorizedHttpResult>(result);
        await commands.DidNotReceiveWithAnyArgs().ChangeDisplayName(default, default!, default);
    }

    [Fact]
    public async Task ChangeDisplayName_UnknownUser_ReturnsUnauthorized()
    {
        var commands = Substitute.For<IUserCommands>();
        commands.ChangeDisplayName(UserId, "Ada", Arg.Any<CancellationToken>()).Returns(false);

        Assert.IsType<UnauthorizedHttpResult>(await Change("Ada", commands));
    }

    [Fact]
    public void ChangeDisplayName_IsMarkedWithIgnoreETag()
    {
        var method = typeof(ProfileEndpoints).GetMethod(nameof(ProfileEndpoints.ChangeDisplayName));

        Assert.NotNull(method!.GetCustomAttribute<IgnoreETagAttribute>());
    }
}
