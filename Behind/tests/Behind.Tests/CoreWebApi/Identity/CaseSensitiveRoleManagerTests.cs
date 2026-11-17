using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Behind.Tests.CoreWebApi.Identity;

public sealed class CaseSensitiveRoleManagerTests
{
    [Fact]
    public async Task RoleLookupPreservesExactName()
    {
        var store = Substitute.For<IRoleStore<ApplicationRole>>();
        using var manager = new CaseSensitiveRoleManager(store, [],
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
            NullLogger<RoleManager<ApplicationRole>>.Instance);

        await manager.FindByNameAsync("Équipe");
        await store.Received(1).FindByNameAsync("Équipe", Arg.Any<CancellationToken>());
        Assert.NotEqual(manager.NormalizeKey("editor"), manager.NormalizeKey("Editor"));
    }
}
