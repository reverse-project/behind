using System.Net;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.Database.Sqlite;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.MagicLinks;
using Behind.Core.WebApi.TwoFactor;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class PasswordResetEndpointsTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
    private static readonly JwtSettings Jwt = new("issuer", "audience", new string('k', 64));
    private static readonly Uuid Secret = Uuid.CreateVersion7(Now.AddHours(-12));

    private static readonly MagicLinkSettings Settings = new(
        Secret, None, Some(TimeSpan.FromDays(30)), Some(TimeSpan.FromDays(1)),
        Some(TimeSpan.FromMinutes(15)), Some(TimeSpan.FromMinutes(1)));

    private readonly TestDbContextFactory _factory = new();
    private readonly CoreContextSqlite _context;
    private readonly AdjustableTimeProvider _time = new(Now);
    private readonly ISessionCommands _sessions = Substitute.For<ISessionCommands>();
    private readonly IWallOfShameQueries _bans = Substitute.For<IWallOfShameQueries>();
    private readonly ITwoFactorGate _gate = Substitute.For<ITwoFactorGate>();
    private readonly IPasswordValidator<ApplicationUser> _validator = Substitute.For<IPasswordValidator<ApplicationUser>>();
    private readonly MagicLinkCommands _commands;
    private readonly MagicLinkQueries _queries;
    private readonly PasswordResetGrants _grants;
    private readonly UserManager<ApplicationUser> _userManager;
    private ApplicationUser _user = null!;

    public PasswordResetEndpointsTests()
    {
        _context = _factory.CreateContext();
        _commands = new MagicLinkCommands(_context);
        _queries = new MagicLinkQueries(_context);
        _grants = new PasswordResetGrants(new MagicLinkSettingsStore(Some(Settings)), Security, _commands, _time);
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        _userManager.PasswordValidators.Add(_validator);
        _validator.ValidateAsync(Arg.Any<UserManager<ApplicationUser>>(), Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);
        _validator.ValidateAsync(Arg.Any<UserManager<ApplicationUser>>(), Arg.Any<ApplicationUser>(), "weak")
            .Returns(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort", Description = "Too short." }));
        _userManager.GeneratePasswordResetTokenAsync(Arg.Any<ApplicationUser>()).Returns("identity-token");
        _userManager.ResetPasswordAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);
    }

    public void Dispose()
    {
        _context.Dispose();
        _factory.Dispose();
    }

    private static IOptions<SecurityConfiguration> Security =>
        Options.Create(new SecurityConfiguration(false, None, Some(Jwt), None));

    private async Task SeedUser()
    {
        await using var seed = _factory.CreateContext();
        var id = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "user@test.com");
        _user = new ApplicationUser { Id = id, DisplayName = "User", Emails = [new Email("user@test.com", Now)] };
        _userManager.FindByIdAsync(id.ToString()).Returns(_user);
    }

    private static DefaultHttpContext NewHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.1.2.3");
        context.Request.Headers.UserAgent = "unit-test";
        return context;
    }

    private async Task<string> EmailedLink(MagicLinkFunction function)
    {
        var link = MagicLink.Create(_time.GetUtcNow(), TimeSpan.FromMinutes(15), function);
        await _commands.Issue(_user.Id, link);
        return MagicLinkCredential.Create(link.Nonce, Secret, Jwt.SigningKey).Token;
    }

    private Task<IResult> Redeem(string token, HttpContext? httpContext = null) =>
        SignInEndpoints.SignInMagicLink(
            _userManager, Security, new MagicLinkSettingsStore(Some(Settings)), _commands, _sessions, _bans, _gate,
            _grants, _time, httpContext ?? NewHttpContext(), NullLogger<SignInEndpoints>.Instance,
            new SignInEndpoints.MagicLinkSignInRequest(token));

    private Task<IResult> Reset(string token, string password = "a new password", HttpContext? httpContext = null) =>
        PasswordResetEndpoints.Reset(
            _userManager, _grants, _queries, _commands, _sessions, _time, httpContext ?? NewHttpContext(),
            NullLogger<PasswordResetEndpoints>.Instance, new PasswordResetEndpoints.PasswordResetRequest(token, password));

    private async Task<string> GrantToken()
    {
        var result = await Redeem(await EmailedLink(MagicLinkFunction.LostPassword));
        return Assert.IsType<JsonHttpResult<PasswordResetEndpoints.PasswordResetResponse>>(result).Value!.ResetToken;
    }

    [Fact]
    public async Task LostPasswordLink_YieldsARestrictedGrantThatResetsThePasswordOnce()
    {
        await SeedUser();
        var httpContext = NewHttpContext();

        var grant = await GrantToken();
        var result = await Reset(grant, httpContext: httpContext);

        Assert.IsType<NoContent>(result);
        Assert.Equal("no-store", httpContext.Response.Headers.CacheControl.ToString());
        await _userManager.Received(1).ResetPasswordAsync(_user, "identity-token", "a new password");
        await _sessions.DidNotReceiveWithAnyArgs().CreateForUserId(default, default!, default);
        Assert.IsType<UnauthorizedHttpResult>(await Reset(grant, "another password"));
        await _userManager.Received(1).ResetPasswordAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Grant_IsNeitherAnAccessNorARefreshToken()
    {
        await SeedUser();

        var grant = await GrantToken();

        Assert.Equal(None, await Jwt.ReadSessionClaims(grant));
    }

    [Fact]
    public async Task SuccessfulReset_RevokesOutstandingLinksAndGrantsAndSignsOutEverySession()
    {
        await SeedUser();
        var pendingSignIn = await EmailedLink(MagicLinkFunction.SignIn);
        var grant = await GrantToken();
        var otherGrant = await GrantToken();

        Assert.IsType<NoContent>(await Reset(grant));

        await _sessions.Received(1).InvalidateAllSessions(_user.Id, _user.Id, Now, Arg.Any<CancellationToken>());
        Assert.IsType<UnauthorizedHttpResult>(await Reset(otherGrant));
        Assert.IsType<UnauthorizedHttpResult>(await Redeem(pendingSignIn));
    }

    [Fact]
    public async Task Grant_Expires()
    {
        await SeedUser();
        var grant = await GrantToken();

        _time.Advance(PasswordResetGrants.Lifetime);

        Assert.IsType<UnauthorizedHttpResult>(await Reset(grant));
        await _userManager.DidNotReceiveWithAnyArgs().ResetPasswordAsync(default!, default!, default!);
    }

    [Fact]
    public async Task RejectedPassword_KeepsTheGrantUsable()
    {
        await SeedUser();
        var grant = await GrantToken();

        var rejected = await Reset(grant, "weak");

        Assert.Equal(new[] { "Too short." }, Assert.IsType<BadRequest<IEnumerable<string>>>(rejected).Value!);
        await _userManager.DidNotReceiveWithAnyArgs().ResetPasswordAsync(default!, default!, default!);
        Assert.IsType<NoContent>(await Reset(grant));
    }

    [Theory]
    [InlineData(MagicLinkFunction.SignIn)]
    [InlineData(MagicLinkFunction.LostPassword)]
    public async Task EmailedLinks_CannotResetThePassword(MagicLinkFunction function)
    {
        await SeedUser();
        var link = await EmailedLink(function);

        Assert.IsType<UnauthorizedHttpResult>(await Reset(link));
        Assert.IsType<UnauthorizedHttpResult>(await Reset("not-a-grant"));
        await _userManager.DidNotReceiveWithAnyArgs().ResetPasswordAsync(default!, default!, default!);
    }

    [Fact]
    public async Task Grant_CannotBeRedeemedAsAMagicLink()
    {
        await SeedUser();
        var grant = await GrantToken();

        Assert.IsType<UnauthorizedHttpResult>(await Redeem(grant));
        await _sessions.DidNotReceiveWithAnyArgs().CreateForUserId(default, default!, default);
        Assert.IsType<NoContent>(await Reset(grant));
    }

    [Fact]
    public async Task SignInLink_StillStartsANormalSession()
    {
        await SeedUser();
        _sessions.CreateForUserId(Arg.Any<Guid>(), Arg.Any<ValidSession>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        var result = await Redeem(await EmailedLink(MagicLinkFunction.SignIn));

        Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(result);
        await _sessions.Received(1).CreateForUserId(_user.Id, Arg.Any<ValidSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnknownUser_IsUnauthorized()
    {
        await SeedUser();
        var grant = await GrantToken();
        _userManager.FindByIdAsync(_user.Id.ToString()).Returns((ApplicationUser?)null);

        Assert.IsType<UnauthorizedHttpResult>(await Reset(grant));
    }
}
