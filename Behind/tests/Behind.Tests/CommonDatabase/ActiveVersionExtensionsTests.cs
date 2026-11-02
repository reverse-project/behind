using Behind.Core.Database;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Behind.Tests.CommonDatabase;

public sealed class ActiveVersionExtensionsTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task SetAsync(string type, string key, long version)
    {
        await using var ctx = _factory.CreateVersioningContext();
        await ctx.SetActiveVersionAsync(type, key, version);
        await ctx.SaveChangesAsync();
    }

    private async Task<IReadOnlyDictionary<string, long>> ReadAsync(string type, params string[] keys)
    {
        await using var ctx = _factory.CreateVersioningContext();
        return await ctx.ActiveVersionsAsync(type, keys);
    }

    [Fact]
    public async Task SetActiveVersion_NewEntity_AddsTheRow()
    {
        await SetAsync("Page", "a", 3);

        Assert.Equal(3L, (await ReadAsync("Page", "a"))["a"]);
    }

    [Fact]
    public async Task SetActiveVersion_ExistingEntity_ReplacesTheVersion()
    {
        await SetAsync("Page", "a", 1);
        await SetAsync("Page", "a", 4);

        Assert.Equal(4L, (await ReadAsync("Page", "a"))["a"]);
    }

    [Fact]
    public async Task SetActiveVersion_SameVersion_ChangesNothing()
    {
        await SetAsync("Page", "a", 2);

        await using var ctx = _factory.CreateVersioningContext();
        await ctx.SetActiveVersionAsync("Page", "a", 2);

        Assert.False(ctx.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task ActiveVersions_AreScopedByEntityTypeAndKey()
    {
        await SetAsync("Page", "a", 1);
        await SetAsync("Page", "b", 2);
        await SetAsync("Other", "a", 9);

        var found = await ReadAsync("Page", "a", "missing");

        Assert.Equal(new Dictionary<string, long> { ["a"] = 1 }, found);
    }

    [Fact]
    public async Task ActiveVersions_AreOwnedByTheVersioningContextAndOnlyReadByTheCoreContext()
    {
        await using var core = _factory.CreateContext();
        await using var versioning = _factory.CreateVersioningContext();

        Assert.True(core.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ActiveVersion))!.IsTableExcludedFromMigrations());
        Assert.False(versioning.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ActiveVersion))!.IsTableExcludedFromMigrations());
        Assert.NotNull(versioning.Model.FindEntityType(typeof(ActiveVersion)));
        Assert.True(versioning.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(AuditLogEntry))!.IsTableExcludedFromMigrations());
    }

    [Fact]
    public async Task VersioningContext_SynchronousSaveIsNotSupported()
    {
        await using var ctx = _factory.CreateVersioningContext();

        Assert.Throws<NotSupportedException>(() => ctx.SaveChanges());
    }
}
