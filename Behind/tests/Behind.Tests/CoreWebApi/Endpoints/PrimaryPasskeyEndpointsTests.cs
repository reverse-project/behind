using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.TwoFactor;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class PrimaryPasskeyEndpointsTests : IDisposable
{
    private readonly TwoFactorTestBed _bed = new();
    private readonly UserManager<ApplicationUser> _users = Substitute.For<UserManager<ApplicationUser>>(
        Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
    private readonly ISessionCommands _sessions = Substitute.For<ISessionCommands>();
    private readonly IWallOfShameQueries _bans = Substitute.For<IWallOfShameQueries>();
    private readonly IConfiguration _configuration = new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["Frontend:Url"] = TwoFactorTestBed.Origin.ToString() }).Build();

    public void Dispose() => _bed.Dispose();

    [Fact]
    public async Task PasskeySignInRequiresADifferentFactorAndRejectsAssertionReplay()
    {
        var user = await _bed.SeedUser();
        using var authenticator = await _bed.EnablePasskey(user);
        _users.FindByEmailAsync("ada@test.com").Returns(user);
        _users.FindByIdAsync(user.Id.ToString()).Returns(user);
        var http = new DefaultHttpContext();
        var commands = new MagicLinkCommands(_bed.Context);
        var result = await PrimaryPasskeyEndpoints.Options(
            new("ada@test.com"), http, _configuration, _users, _bed.Verification, _bed.Ceremonies,
            _bed.Tickets, commands, _bed.Time);
        var challenge = Assert.IsType<JsonHttpResult<PasskeyAssertionChallenge>>(result).Value!;
        var credential = authenticator.Assert(challenge.Options, TwoFactorTestBed.OriginText);
        var security = Options.Create(new SecurityConfiguration(false, None,
            Some(new JwtSettings("issuer", "audience", new string('k', 64))), None));

        Task<IResult> Verify() => PrimaryPasskeyEndpoints.Verify(
            new(challenge.Ticket, credential), http, _configuration, _users, security, _bed.Verification,
            _bed.Tickets, commands, _sessions, _bans, _bed.Gate, _bed.Time,
            NullLogger<PrimaryPasskeyEndpoints>.Instance);

        var first = await Verify();
        var second = Assert.IsType<JsonHttpResult<TwoFactorChallengeResponse>>(first).Value!;
        Assert.DoesNotContain(TwoFactorMethod.Passkey, second.Methods);
        Assert.Contains(TwoFactorMethod.Email, second.Methods);
        Assert.Contains(TwoFactorMethod.Recovery, second.Methods);
        Assert.IsType<UnauthorizedHttpResult>(await Verify());
        await _sessions.DidNotReceiveWithAnyArgs().CreateForUserId(default, default!, default);
    }

    [Fact]
    public async Task UnknownAccountReceivesAChallengeWithoutAccountDisclosure()
    {
        var response = await PrimaryPasskeyEndpoints.Options(
            new("unknown@test.com"), new DefaultHttpContext(), _configuration, _users, _bed.Verification,
            _bed.Ceremonies, _bed.Tickets, new MagicLinkCommands(_bed.Context), _bed.Time);

        var challenge = Assert.IsType<JsonHttpResult<PasskeyAssertionChallenge>>(response).Value!;
        Assert.Single(challenge.Options.AllowCredentials);
        Assert.NotEmpty(challenge.Ticket);
    }
}
