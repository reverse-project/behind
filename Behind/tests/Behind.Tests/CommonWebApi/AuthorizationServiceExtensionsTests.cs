using System.Security.Claims;
using Behind.Common.WebApi;

namespace Behind.Tests.CommonWebApi;

public class AuthorizationServiceExtensionsTests
{
    [Fact]
    public async Task HoldsGrantAsync_IsTrueOnlyForTheGrantsTheServiceApproves()
    {
        var authorization = new FakeAuthorizationService("Quotes.Read");

        Assert.True(await authorization.HoldsGrantAsync(new ClaimsPrincipal(), "Quotes.Read"));
        Assert.False(await authorization.HoldsGrantAsync(new ClaimsPrincipal(), "Quotes.Write"));
    }
}
