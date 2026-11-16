using Behind.Core.Database;
using Behind.Core.WebApi.Middleware;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;

namespace Behind.Tests.CoreWebApi.Middleware;

public class AccessGateTests
{
    private static readonly Guid SessionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly IAccessSettingsQueries _settings = Substitute.For<IAccessSettingsQueries>();
    private readonly IAccessQueries _access = Substitute.For<IAccessQueries>();

    private AccessGate Gate(bool signedIn = true) =>
        new(_settings, SessionTestHelper.SessionQueries(SessionId, UserId, signedIn), _access);

    private void Configure(AccessSettings settings) =>
        _settings.Current(Arg.Any<CancellationToken>()).Returns(settings);

    private void GiveRoles(params string[] roles) =>
        _access.GetUserAccess(UserId, Arg.Any<CancellationToken>())
            .Returns(new UserAccess(roles.Select(role => (Role)new UserRole(role)).ToList(), [], []));

    [Fact]
    public async Task Decide_RestrictionOff_IsOpenWithoutLookingAtTheVisitor()
    {
        Configure(AccessSettings.Open);

        var decision = await Gate().Decide(SessionTestHelper.SignedIn(SessionId));

        Assert.IsType<AccessDecision.Open>(decision);
        await _access.DidNotReceiveWithAnyArgs().GetUserAccess(default, default);
    }

    [Fact]
    public async Task Decide_AnonymousVisitor_IsRestrictedWithTheMessage()
    {
        Configure(new AccessSettings(true, "Users", "Members only", false));

        var decision = await Gate().Decide(new DefaultHttpContext());

        Assert.Equal("Members only", Assert.IsType<AccessDecision.Restricted>(decision).Message);
    }

    [Fact]
    public async Task Decide_UserWithTheRequiredRole_IsOpen()
    {
        Configure(new AccessSettings(true, "Users", "Members only", false));
        GiveRoles("Users");

        Assert.IsType<AccessDecision.Open>(await Gate().Decide(SessionTestHelper.SignedIn(SessionId)));
    }

    [Fact]
    public async Task Decide_UserWithoutTheRequiredRole_IsRestricted()
    {
        Configure(new AccessSettings(true, "Members", "Members only", false));
        GiveRoles("Users");

        Assert.IsType<AccessDecision.Restricted>(await Gate().Decide(SessionTestHelper.SignedIn(SessionId)));
    }

    [Fact]
    public async Task Decide_InvalidSession_IsRestricted()
    {
        Configure(new AccessSettings(true, "Users", "Members only", false));
        GiveRoles("Users");

        Assert.IsType<AccessDecision.Restricted>(
            await Gate(signedIn: false).Decide(SessionTestHelper.SignedIn(SessionId)));
    }

    [Fact]
    public async Task Decide_AccessSettingsManager_IsOpenWithoutTheRole()
    {
        Configure(new AccessSettings(true, "Members", "Members only", false));
        _access.GetUserAccess(UserId, Arg.Any<CancellationToken>())
            .Returns(new UserAccess([], [new Grant(AccessSettingsExtensions.ManageGrant)], []));

        Assert.IsType<AccessDecision.Open>(await Gate().Decide(SessionTestHelper.SignedIn(SessionId)));
    }
}
