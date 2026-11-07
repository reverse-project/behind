using Behind.Core.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi.Authorization;

public class GrantAuthorizationExtensionsTests
{
    [Fact]
    public async Task AddGrantAuthorization_RegistersAPolicyForEveryBuiltInGrant()
    {
        var provider = new ServiceCollection()
            .AddLogging()
            .AddGrantAuthorization()
            .BuildServiceProvider();
        var policies = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        foreach (var grant in BuiltInAccess.Grants)
        {
            var policy = await policies.GetPolicyAsync(grant.Grant.Name);

            Assert.Contains(policy!.Requirements, r => r is GrantRequirement { Grant: var name } && name == grant.Grant.Name);
        }
    }

    [Fact]
    public async Task AddGrantAuthorization_RegistersAPolicyForEveryGrantOfTheModules()
    {
        var provider = new ServiceCollection()
            .AddLogging()
            .AddModule(NotesModule.Descriptor)
            .AddGrantAuthorization()
            .BuildServiceProvider();
        var policies = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        foreach (var action in GrantAction.All)
        {
            var name = GrantName.Of(NotesModule.Area, action);
            var policy = await policies.GetPolicyAsync(name);

            Assert.Contains(policy!.Requirements, r => r is GrantRequirement { Grant: var grant } && grant == name);
        }
    }

    [Fact]
    public async Task AddGrantAuthorization_DoesNotRegisterModuleGrantsWithoutTheModule()
    {
        var provider = new ServiceCollection()
            .AddLogging()
            .AddGrantAuthorization()
            .BuildServiceProvider();

        Assert.Null(await provider.GetRequiredService<IAuthorizationPolicyProvider>().GetPolicyAsync("Notes.Read"));
    }
}
