using Microsoft.Extensions.Hosting;

namespace Behind.Tests.Helpers;

public static class FakeHostEnvironment
{
    public static IHostEnvironment Named(string environmentName)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        return environment;
    }

    public static IHostEnvironment Development => Named(Environments.Development);

    public static IHostEnvironment Production => Named(Environments.Production);
}
