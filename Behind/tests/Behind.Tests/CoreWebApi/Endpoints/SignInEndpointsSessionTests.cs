using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Behind.Common;
using Behind.Core.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class SignInEndpointsSessionTests
{
    private static readonly JwtSettings Jwt = new("issuer", "audience", new string('k', 64));

    private static IOptions<SecurityConfiguration> Security(Option<JwtSettings> jwt) =>
        Options.Create(new SecurityConfiguration(false, None, jwt, None));

    private static UserManager<ApplicationUser> UserManagerFor(ApplicationUser? user)
    {
        var manager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        manager.FindByEmailAsync(Arg.Any<string>()).Returns(user);
        return manager;
    }

    private static ApplicationUser NewUser(string password) =>
        new()
        {
            DisplayName = "User",
            Emails = [new Email("user@test.com")],
            Password = PasswordHash.Create(password)
        };

    private static DefaultHttpContext NewHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.1.2.3");
        context.Request.Headers.UserAgent = "unit-test";
        return context;
    }

    [Fact]
    public async Task SignInPassword_ValidCredentials_PersistsSessionAndBindsTokens()
    {
        var user = NewUser("secret");
        var sessionId = Guid.NewGuid();
        var commands = Substitute.For<ISessionCommands>();
        commands.CreateForUserId(Arg.Any<Guid>(), Arg.Any<ValidSession>(), Arg.Any<CancellationToken>())
            .Returns(sessionId);

        var result = await SignInEndpoints.SignInPassword(
            UserManagerFor(user),
            Security(Some(Jwt)),
            commands,
            NewHttpContext(),
            new SignInEndpoints.PasswordRequest("user@test.com", "secret"));

        var json = Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(result);
        var persisted = Assert.IsType<ValidSession>(
            commands.ReceivedCalls().Single().GetArguments()[1]);
        await commands.Received(1).CreateForUserId(user.Id, persisted, Arg.Any<CancellationToken>());

        Assert.Equal("10.1.2.3", persisted.RemoteAddress.Value);
        Assert.Equal("unit-test", persisted.UserAgent.Value);

        var access = new JwtSecurityTokenHandler().ReadJwtToken(json.Value!.AccessToken);
        var refresh = new JwtSecurityTokenHandler().ReadJwtToken(json.Value.RefreshToken);
        Assert.Equal(sessionId.ToString(), access.Claims.Single(c => c.Type == SessionClaimTypes.SessionId).Value);
        Assert.Equal(sessionId.ToString(), refresh.Claims.Single(c => c.Type == SessionClaimTypes.SessionId).Value);
        Assert.Equal(
            persisted.AccessToken.Nonce.ToString(),
            access.Claims.Single(c => c.Type == SessionClaimTypes.TokenNonce).Value);
        Assert.Equal(
            persisted.RefreshToken.Nonce.ToString(),
            refresh.Claims.Single(c => c.Type == SessionClaimTypes.TokenNonce).Value);
    }

    [Fact]
    public async Task SignInPassword_WrongPassword_ReturnsUnauthorizedAndPersistsNothing()
    {
        var commands = Substitute.For<ISessionCommands>();

        var result = await SignInEndpoints.SignInPassword(
            UserManagerFor(NewUser("secret")),
            Security(Some(Jwt)),
            commands,
            NewHttpContext(),
            new SignInEndpoints.PasswordRequest("user@test.com", "wrong"));

        Assert.IsType<UnauthorizedHttpResult>(result);
        await commands.DidNotReceiveWithAnyArgs()
            .CreateForUserId(default, default!, default);
    }

    [Fact]
    public async Task SignInPassword_UnknownUser_ReturnsUnauthorized()
    {
        var result = await SignInEndpoints.SignInPassword(
            UserManagerFor(null),
            Security(Some(Jwt)),
            Substitute.For<ISessionCommands>(),
            NewHttpContext(),
            new SignInEndpoints.PasswordRequest("user@test.com", "secret"));

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task SignInPassword_WithoutJwtSettings_ReturnsServerError()
    {
        var result = await SignInEndpoints.SignInPassword(
            UserManagerFor(NewUser("secret")),
            Security(None),
            Substitute.For<ISessionCommands>(),
            NewHttpContext(),
            new SignInEndpoints.PasswordRequest("user@test.com", "secret"));

        Assert.IsType<InternalServerError>(result);
    }
}
