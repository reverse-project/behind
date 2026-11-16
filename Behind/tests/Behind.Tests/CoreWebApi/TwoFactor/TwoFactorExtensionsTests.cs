using Behind.Core.WebApi.TwoFactor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi.TwoFactor;

public class TwoFactorExtensionsTests
{
    private static IConfiguration Frontend(string? url) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(url is null ? [] : new Dictionary<string, string?> { ["Frontend:Url"] = url })
            .Build();

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
        var origin = Frontend("https://www.example.test/app/").PasskeyOrigin();

        Assert.Equal("https://www.example.test", origin.GetLeftPart(UriPartial.Authority));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("/relative")]
    [InlineData("file:///tmp/frontend")]
    [InlineData("https://user:password@app.test/")]
    [InlineData("https://app.test/?query=1")]
    [InlineData("https://app.test/#fragment")]
    public void PasskeyOrigin_RequiresValidConfiguredFrontendAddress(string? url)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Frontend(url).PasskeyOrigin());

        Assert.Contains("Frontend:Url", exception.Message);
    }

    [Fact]
    public void PasskeyOrigin_UsesCurrentConfigurationAndFailsWhenRemoved()
    {
        var configuration = Frontend("https://app.test/");
        Assert.Equal("https://app.test/", configuration.PasskeyOrigin().AbsoluteUri);
        configuration["Frontend:Url"] = "https://new-app.test/frontend/";
        Assert.Equal("https://new-app.test/frontend/", configuration.PasskeyOrigin().AbsoluteUri);
        configuration["Frontend:Url"] = null;
        Assert.Throws<InvalidOperationException>(() => configuration.PasskeyOrigin());
    }
}
