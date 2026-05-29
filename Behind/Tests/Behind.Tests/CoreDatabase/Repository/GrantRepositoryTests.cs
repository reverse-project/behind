using Behind.Core.Database;
using Behind.Core.Database.Sqlite;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase.Repository;

public sealed class GrantRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task TryFindAsync_ExistingId_ReturnsSomeWithCorrectName()
    {
        using var seed = _factory.CreateContext();
        var id = await DatabaseSeedHelper.SeedGrantAsync(seed, "grant-find");

        using var ctx = _factory.CreateContext();
        var result = await ((IRepository<Grant, Guid>)ctx).TryFindAsync(id);

        var found = false;
        result.Match(() => { }, g => { found = true; Assert.Equal("grant-find", g.Name); });
        Assert.True(found);
    }

    [Fact]
    public async Task TryFindAsync_UnknownId_ReturnsNone()
    {
        using var ctx = _factory.CreateContext();
        var result = await ((IRepository<Grant, Guid>)ctx).TryFindAsync(Guid.NewGuid());
        var isNone = true;
        result.Match(() => { }, _ => isNone = false);
        Assert.True(isNone);
    }

    [Fact]
    public async Task AddAsync_Persists_Grant()
    {
        Guid id;
        using (var ctx = _factory.CreateContext())
        {
            var grant = new Grant("persisted-grant");
            var repo = (IRepository<Grant, Guid>)ctx;
            await repo.AddAsync(grant);
            id = ctx.Entry(grant).Property<Guid>("Id").CurrentValue;
            await ctx.SaveChangesAsync();
        }

        using var read = _factory.CreateContext();
        var found = false;
        (await ((IRepository<Grant, Guid>)read).TryFindAsync(id))
            .Match(() => { }, g => { found = true; Assert.Equal("persisted-grant", g.Name); });
        Assert.True(found);
    }

    [Fact]
    public async Task GetKey_AfterAdd_ReturnsNonEmptyGuid()
    {
        using var ctx = _factory.CreateContext();
        var grant = new Grant("getkey-grant");
        await ctx.Grants.AddAsync(grant);
        var key = ((IRepository<Grant, Guid>)ctx).GetKey(grant);
        Assert.NotEqual(Guid.Empty, key);
    }

    [Fact]
    public async Task Delete_RemovesGrant_FromDatabase()
    {
        Guid id;
        using (var seed = _factory.CreateContext())
            id = await DatabaseSeedHelper.SeedGrantAsync(seed, "del-grant");

        using (var ctx = _factory.CreateContext())
        {
            var tracked = await ctx.Grants
                .Where(g => EF.Property<Guid>(g, "Id") == id)
                .FirstAsync();
            ((IRepository<Grant, Guid>)ctx).Delete(tracked);
            await ctx.SaveChangesAsync();
        }

        using var verify = _factory.CreateContext();
        var isNone = true;
        (await ((IRepository<Grant, Guid>)verify).TryFindAsync(id))
            .Match(() => { }, _ => isNone = false);
        Assert.True(isNone);
    }
}
