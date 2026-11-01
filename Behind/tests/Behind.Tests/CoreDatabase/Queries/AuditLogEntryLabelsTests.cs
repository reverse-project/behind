using Behind.Core.Database;
using Behind.Core.Database.Queries;
using Behind.Tests.Helpers;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class AuditLogEntryLabelsTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task ResolveLabels_NoIdentifiers_ReturnsNoLabels()
    {
        await using var ctx = _factory.CreateContext();

        var labels = await new AuditLogEntryQueries(ctx).ResolveLabels([]);

        Assert.Empty(labels.Values);
    }

    [Fact]
    public async Task ResolveLabels_NamesUsersEmailsRolesAndGrants()
    {
        Guid userId;
        Guid emailId;
        Guid roleId;
        Guid grantId;
        await using (var seed = _factory.CreateContext())
        {
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "ada@test.com");
            emailId = await DatabaseSeedHelper.SeedEmailAsync(seed, userId, "ada2@test.com");
            roleId = await DatabaseSeedHelper.SeedUserRoleAsync(seed, "Editors");
            grantId = await DatabaseSeedHelper.SeedGrantAsync(seed, "Posts.Write");
        }

        await using var ctx = _factory.CreateContext();
        var labels = await new AuditLogEntryQueries(ctx).ResolveLabels([userId, emailId, roleId, grantId]);

        Assert.Equal("Test User", labels.Label(userId.ToString()));
        Assert.Equal("ada2@test.com", labels.Label(emailId.ToString()));
        Assert.Equal("Editors", labels.Label(roleId.ToString()));
        Assert.Equal("Posts.Write", labels.Label(grantId.ToString()));
        Assert.Equal("Editors: Posts.Write", labels.LabelKey($"{roleId}:{grantId}"));
    }

    [Fact]
    public async Task ResolveLabels_NamesBuiltInUsers()
    {
        await using var ctx = _factory.CreateContext();

        var labels = await new AuditLogEntryQueries(ctx)
            .ResolveLabels([DomainConstants.SystemUserId, DomainConstants.GuestUserId]);

        Assert.Equal("System", labels.Label(DomainConstants.SystemUserId.ToString()));
        Assert.Equal("Guest", labels.Label(DomainConstants.GuestUserId.ToString()));
    }

    [Fact]
    public async Task ResolveLabels_NamesOwnedEntitiesAfterTheirUser()
    {
        Guid userId;
        Guid shameId;
        await using (var seed = _factory.CreateContext())
        {
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "owner@test.com");
            shameId = await DatabaseSeedHelper.SeedWallOfShameEntryAsync(seed, userId, userId);
        }

        await using var ctx = _factory.CreateContext();
        var labels = await new AuditLogEntryQueries(ctx).ResolveLabels([shameId]);

        Assert.Equal("Wall of shame entry of Test User", labels.Label(shameId.ToString()));
    }

    [Fact]
    public async Task ResolveLabels_UnknownIdentifierIsKeptAsIs()
    {
        var unknown = Guid.NewGuid();
        await using var ctx = _factory.CreateContext();

        var labels = await new AuditLogEntryQueries(ctx).ResolveLabels([unknown]);

        Assert.Equal(unknown.ToString(), labels.Label(unknown.ToString()));
        Assert.Equal("plain", labels.Label("plain"));
        Assert.Null(labels.LabelOrNull(null));
    }
}
