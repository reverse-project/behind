using System.Reflection;
using Behind.Common.WebApi.Middleware;
using Behind.Core.WebApi.Endpoints;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class IdentityEndpointsETagTests
{
    [Fact]
    public void SignUpPassword_IsMarkedWithIgnoreETag()
    {
        var method = typeof(SignUpEndpoints).GetMethod(nameof(SignUpEndpoints.SignUpPassword));

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<IgnoreETagAttribute>());
    }

    [Fact]
    public void ResendConfirmation_IsMarkedWithIgnoreETag()
    {
        var method = typeof(SignUpEndpoints).GetMethod(nameof(SignUpEndpoints.ResendConfirmation));

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<IgnoreETagAttribute>());
    }

    [Fact]
    public void SignInPassword_IsMarkedWithIgnoreETag()
    {
        var method = typeof(SignInEndpoints).GetMethod(nameof(SignInEndpoints.SignInPassword));

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<IgnoreETagAttribute>());
    }
}
