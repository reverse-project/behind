using Behind.Core.WebApi.TwoFactor;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi.TwoFactor;

public class TwoFactorExtensionsTests
{
    private static IConfiguration Frontend(string? url) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(url is null ? [] : new Dictionary<string, string?> { ["Frontend:Url"] = url })
            .Build();

    private static DefaultHttpContext Request()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("api.test", 8443);

        return context;
    }

    [Theory]
    [InlineData(typeof(ITwoFactorTickets), typeof(TwoFactorTickets), ServiceLifetime.Singleton)]
    [InlineData(typeof(IPasskeyCeremonies), typeof(PasskeyCeremonies), ServiceLifetime.Singleton)]
    [InlineData(typeof(ITwoFactorGate), typeof(TwoFactorGate), ServiceLifetime.Scoped)]
    [InlineData(typeof(ITwoFactorSignIn), typeof(TwoFactorSignIn), ServiceLifetime.Scoped)]
    [InlineData(typeof(IRecoveryCodes), typeof(RecoveryCodes), ServiceLifetime.Scoped)]
    [InlineData(typeof(ITwoFactorVerification), typeof(TwoFactorVerification), ServiceLifetime.Scoped)]
    [InlineData(typeof(ITwoFactorEnrollment), typeof(TwoFactorEnrollment), ServiceLifetime.Scoped)]
    public void AddTwoFactor_RegistersTheService(Type service, Type implementation, ServiceLifetime lifetime)
    {
        var services = new ServiceCollection().AddTwoFactor();

        var descriptor = Assert.Single(services, candidate => candidate.ServiceType == service);
        Assert.Equal(implementation, descriptor.ImplementationType);
        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    [Fact]
    public void AddTwoFactor_KeepsAServiceRegisteredBefore()
    {
        var signIn = Substitute.For<ITwoFactorSignIn>();
        var services = new ServiceCollection();
        services.AddScoped(_ => signIn);

        services.AddTwoFactor();

        Assert.Single(services, candidate => candidate.ServiceType == typeof(ITwoFactorSignIn));
    }

    [Fact]
    public void PasskeyOrigin_IsTheConfiguredFrontendAddress()
    {
        var origin = Request().PasskeyOrigin(Frontend("https://www.example.test/app/"));

        Assert.Equal("https://www.example.test", origin.GetLeftPart(UriPartial.Authority));
    }

    [Fact]
    public void PasskeyOrigin_FallsBackToTheAddressOfTheRequest()
    {
        var origin = Request().PasskeyOrigin(Frontend(null));

        Assert.Equal("https://api.test:8443", origin.GetLeftPart(UriPartial.Authority));
    }
}
