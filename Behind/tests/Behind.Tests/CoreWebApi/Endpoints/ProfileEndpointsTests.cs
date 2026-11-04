using System.Reflection;
using System.Security.Claims;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

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

    private static ISessionQueries SessionQueries(bool valid, bool impersonated = false)
    {
        var queries = Substitute.For<ISessionQueries>();
        queries.TryFindByIdWithUserId(SessionId, Arg.Any<CancellationToken>())
            .Returns(valid
                ? Some<(Session, Guid)>((
                    new ValidSession(
                        AccessToken.Create(new GuestUser(None)),
                        RefreshToken.Create(new GuestUser(None)),
                        "10.0.0.1",
                        "test-agent")
                    {
                        Impersonator = impersonated ? Some<User>(new GuestUser(None)) : None
                    },
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

    private static UserManager<ApplicationUser> UserManagerFor(ApplicationUser? user, IdentityResult? reset = null)
    {
        var manager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(),
            null, null, null, null, null, null, null, null);
        manager.FindByIdAsync(UserId.ToString()).Returns(user);
        manager.GeneratePasswordResetTokenAsync(Arg.Any<ApplicationUser>()).Returns("the token");
        manager.ResetPasswordAsync(Arg.Any<ApplicationUser>(), "the token", Arg.Any<string>())
            .Returns(reset ?? IdentityResult.Success);
        return manager;
    }

    private static Task<IResult> ChangePassword(
        UserManager<ApplicationUser> manager,
        ISessionQueries? queries = null) =>
        ProfileEndpoints.ChangePassword(
            new ProfileEndpoints.NewPasswordRequest("N3w passw0rd"),
            SignedIn(),
            queries ?? SessionQueries(true),
            manager,
            NullLogger<ProfileEndpoints>.Instance);

    [Fact]
    public async Task ChangePassword_ValidSession_ResetsThePasswordOfTheSessionUser()
    {
        var user = new ApplicationUser();
        var manager = UserManagerFor(user);

        var result = await ChangePassword(manager);

        Assert.IsType<NoContent>(result);
        await manager.Received(1).ResetPasswordAsync(user, "the token", "N3w passw0rd");
    }

    [Fact]
    public async Task ChangePassword_RejectedPassword_ReturnsBadRequestWithTheErrors()
    {
        var manager = UserManagerFor(
            new ApplicationUser(),
            IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort", Description = "Too short" }));

        var result = await ChangePassword(manager);

        var badRequest = Assert.IsType<BadRequest<IEnumerable<string>>>(result);
        Assert.Equal(["Too short"], badRequest.Value);
    }

    [Fact]
    public async Task ChangePassword_WithoutValidSession_ReturnsUnauthorized()
    {
        var manager = UserManagerFor(new ApplicationUser());

        var result = await ChangePassword(manager, SessionQueries(false));

        Assert.IsType<UnauthorizedHttpResult>(result);
        await manager.DidNotReceiveWithAnyArgs().ResetPasswordAsync(default!, default!, default!);
    }

    [Fact]
    public async Task ChangePassword_UnknownUser_ReturnsUnauthorized()
    {
        var manager = UserManagerFor(null);

        Assert.IsType<UnauthorizedHttpResult>(await ChangePassword(manager));
    }

    [Fact]
    public async Task ChangePassword_ImpersonationSession_ReturnsForbidden()
    {
        var manager = UserManagerFor(new ApplicationUser());

        var result = await ChangePassword(manager, SessionQueries(true, impersonated: true));

        Assert.IsType<ForbidHttpResult>(result);
        await manager.DidNotReceiveWithAnyArgs().ResetPasswordAsync(default!, default!, default!);
    }

    [Fact]
    public void ChangePassword_IsMarkedWithIgnoreETag()
    {
        var method = typeof(ProfileEndpoints).GetMethod(nameof(ProfileEndpoints.ChangePassword));

        Assert.NotNull(method!.GetCustomAttribute<IgnoreETagAttribute>());
    }
}
