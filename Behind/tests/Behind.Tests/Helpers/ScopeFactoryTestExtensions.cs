using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.Helpers;

public static class ScopeFactoryTestExtensions
{
    extension<T>(T service) where T : class
    {
        public IServiceScopeFactory InScopeFactory() =>
            new ServiceCollection()
                .AddSingleton(service)
                .BuildServiceProvider()
                .GetRequiredService<IServiceScopeFactory>();
    }
}
