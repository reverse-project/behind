using System.Security.Claims;
using Behind.Core.Database.Queries;
using Behind.Core.WebApi.Middleware;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;

namespace Behind.Tests.CoreWebApi.Middleware;

public sealed class DeletedRecordsVisibilityMiddlewareTests : IDisposable
{
    private const string Email = "viewer@test.com";

    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task SeedUserWithGrantAsync(string grant)
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, Email);
        var roleId = await DatabaseSeedHelper.SeedUserRoleAsync(seed, "role");
        var grantId = await DatabaseSeedHelper.SeedGrantAsync(seed, grant);
        await DatabaseSeedHelper.AssignRoleToUserAsync(seed, userId, roleId);
        await DatabaseSeedHelper.AssignGrantToRoleAsync(seed, roleId, grantId);
    }

    private static DefaultHttpContext Authenticated(string email) =>
        new() { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, email)], "test")) };

    private async Task<(bool DuringRequest, bool AfterRequest)> InvokeAsync(DefaultHttpContext context)
    {
        var duringRequest = false;
        await using var db = _factory.CreateContext();
        var middleware = new DeletedRecordsVisibilityMiddleware(_ =>
        {
            duringRequest = db.SoftDelete.IncludesDeleted;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, db, new UserQueries(db));

        return (duringRequest, db.SoftDelete.IncludesDeleted);
    }

    [Fact]
    public async Task InvokeAsync_UserWithViewDeletedGrant_IncludesDeletedOnlyDuringRequest()
    {
        await SeedUserWithGrantAsync(DomainConstants.ViewDeletedGrant);

        var (during, after) = await InvokeAsync(Authenticated(Email));

        Assert.True(during);
        Assert.False(after);
    }

    [Fact]
    public async Task InvokeAsync_UserWithoutViewDeletedGrant_DoesNotIncludeDeleted()
    {
        await SeedUserWithGrantAsync("read");

        var (during, _) = await InvokeAsync(Authenticated(Email));

        Assert.False(during);
    }

    [Fact]
    public async Task InvokeAsync_AnonymousRequest_DoesNotIncludeDeleted()
    {
        await SeedUserWithGrantAsync(DomainConstants.ViewDeletedGrant);

        var (during, _) = await InvokeAsync(new DefaultHttpContext());

        Assert.False(during);
    }

    [Fact]
    public async Task InvokeAsync_NextThrows_ReleasesTheSwitch()
    {
        await SeedUserWithGrantAsync(DomainConstants.ViewDeletedGrant);
        await using var db = _factory.CreateContext();
        var middleware = new DeletedRecordsVisibilityMiddleware(_ => throw new InvalidOperationException());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.InvokeAsync(Authenticated(Email), db, new UserQueries(db)));

        Assert.False(db.SoftDelete.IncludesDeleted);
    }
}
