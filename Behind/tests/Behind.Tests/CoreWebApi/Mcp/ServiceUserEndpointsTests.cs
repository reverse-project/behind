using Behind.Common;
using Behind.Common.Configuration;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Mcp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Behind.Tests.CoreWebApi.Mcp;

public sealed class ServiceUserEndpointsTests
{
    private static (HttpContext Context, UserManagementGate Gate, IAccessQueries Access) Actor(bool developer = false)
    {
        var identity = ServiceUserIdentity.Create(
            new ServiceUserEntry(Guid.NewGuid(), 1, new ServiceUser("actor", "", true)), DateTimeOffset.UtcNow);
        var context = new DefaultHttpContext { User = identity.Principal };
        var sessions = Substitute.For<ISessionQueries>();
        sessions.TryFindByIdWithUserId(identity.SessionId, default)
            .Returns(Some<(Session, Guid)>((identity.Session, identity.ServiceUser.Id)));
        var access = Substitute.For<IAccessQueries>();
        IReadOnlyList<Grant> grants = developer
            ? [new("Configuration.Manage"), new("User.Manage"), new(ManagedRole.Developer.RequiredGrant)]
            : [new("Configuration.Manage"), new("User.Manage")];
        access.GetUserAccess(identity.ServiceUser.Id, default).Returns(new UserAccess([], grants, []));
        var gate = new UserManagementGate(sessions, access, Substitute.For<IUserManagementQueries>(), AccessCatalog.Core,
            TimeProvider.System, NullLogger<UserManagementGate>.Instance);
        return (context, gate, access);
    }

    [Fact]
    public async Task CreateReturnsAnIndependentResourceAndRevealsTheKeyOnlyInTheCredentialResponse()
    {
        var (context, gate, _) = Actor();
        var entry = new ServiceUserEntry(Guid.NewGuid(), 1, new ServiceUser("agent", "hash", true));
        var store = Substitute.For<IServiceUserStore>();
        store.Create("agent", default).Returns(Some(new ServiceUserCredential(entry, "once")));
        var result = await ServiceUserEndpoints.Create(new CreateServiceUserRequest(" agent "), context, gate, store,
            Options.Create(new SecurityConfiguration()), default);
        var created = Assert.IsType<Created<ServiceUserCredentialResponse>>(result);
        Assert.Equal($"/service-users/{entry.Id}", created.Location);
        Assert.Equal(entry.Id, created.Value!.User.Id);
        Assert.Equal("once", created.Value.ApiKey);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        store.Find(entry.Id, default).Returns(Some(entry));
        var fetched = Assert.IsType<Ok<ServiceUserResponse>>(
            await ServiceUserEndpoints.Get(entry.Id, context, store, default));
        Assert.Equal(created.Value.User, fetched.Value);
        Assert.True(JsonSchemas.IsValid("service-users-1.0.0",
            JsonSerializer.SerializeToNode(fetched.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
    }

    [Fact]
    public async Task AccessCannotGrantPermissionsTheActorDoesNotHold()
    {
        var (context, gate, access) = Actor();
        var entry = new ServiceUserEntry(Guid.NewGuid(), 1, new ServiceUser("agent", "hash", true));
        var store = Substitute.For<IServiceUserStore>();
        store.Find(entry.Id, default).Returns(Some(entry));
        access.GetUserAccess(entry.Id, default).Returns(UserAccess.Empty);
        var roles = Substitute.For<IRoleQueries>();
        roles.ListRoles(default).Returns([new IRoleQueries.RoleResult(BuiltInAccess.Users.Id, BuiltInAccess.Users.Role)]);
        var commands = Substitute.For<IAccessCommands>();
        var result = await ServiceUserEndpoints.SetAccess(entry.Id,
            new ServiceUserAccessRequest(1, [BuiltInAccess.Users.Id], ["News.Write"], []),
            context, gate, store, access, roles, commands, Options.Create(new SecurityConfiguration()), default);
        Assert.Equal(403, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await commands.DidNotReceiveWithAnyArgs().SetUserAccess(default, default!, default!, default);
    }

    [Fact]
    public async Task ReadOnlyConfigurationBlocksCredentialCreation()
    {
        var (context, gate, _) = Actor();
        var store = Substitute.For<IServiceUserStore>();
        var result = await ServiceUserEndpoints.Create(new CreateServiceUserRequest("agent"), context, gate, store,
            Options.Create(new SecurityConfiguration { ReadOnlyConfiguration = true }), default);
        Assert.Equal(403, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await store.DidNotReceiveWithAnyArgs().Create(default!, default);
    }

    [Fact]
    public async Task ListDisablesManagementActionsForReadOnlyConfiguration()
    {
        var (context, gate, _) = Actor(developer: true);
        var store = Substitute.For<IServiceUserStore>();
        store.List(false, default).Returns([]);

        var result = await ServiceUserEndpoints.List(context, gate, store,
            Options.Create(new SecurityConfiguration { ReadOnlyConfiguration = true }), default);

        var list = Assert.IsType<Ok<ServiceUserListResponse>>(result);
        Assert.False(list.Value!.CanManage);
        Assert.False(list.Value.CanDelete);
        Assert.True(list.Value.IsDeveloper);
    }

    [Fact]
    public async Task RestoreDistinguishesStaleVersionsFromMissingServiceUsers()
    {
        var (context, gate, _) = Actor(developer: true);
        var id = Guid.NewGuid();
        var store = Substitute.For<IServiceUserStore>();
        store.List(true, default).Returns([new ServiceUserEntry(id, 2, new ServiceUser("agent", "hash", false))]);
        var security = Options.Create(new SecurityConfiguration());

        var stale = await ServiceUserEndpoints.Restore(id, new ServiceUserVersionRequest(1),
            context, gate, store, security, default);
        Assert.Equal(409, Assert.IsAssignableFrom<IStatusCodeHttpResult>(stale).StatusCode);
        await store.DidNotReceive().Restore(id, 1, default);

        store.List(true, default).Returns([]);
        var missing = await ServiceUserEndpoints.Restore(id, new ServiceUserVersionRequest(2),
            context, gate, store, security, default);
        Assert.Equal(404, Assert.IsAssignableFrom<IStatusCodeHttpResult>(missing).StatusCode);
        await store.DidNotReceive().Restore(id, 2, default);
    }
}
