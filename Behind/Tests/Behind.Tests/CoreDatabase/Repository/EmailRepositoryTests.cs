using Behind.Core.Database;
using Behind.Core.Database.Sqlite;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase.Repository;

public sealed class EmailRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    // -----------------------------------------------------------------------
    // TryFindAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TryFindAsync_ExistingId_ReturnsSomeWithCorrectValue()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "find@test.com");
        var emailId = await DatabaseSeedHelper.SeedEmailAsync(seed, userId, "extra@test.com");

        await using var ctx = _factory.CreateContext();
        var repo = (IRepository<Email, Guid>)ctx;
        var result = await repo.TryFindAsync(emailId);

        var found = false;
        result.Match(() => { }, e => { found = true; Assert.Equal("extra@test.com", e.Value); });
        Assert.True(found);
    }

    [Fact]
    public async Task TryFindAsync_UnknownId_ReturnsNone()
    {
        await using var ctx = _factory.CreateContext();
        var repo = (IRepository<Email, Guid>)ctx;
        var result = await repo.TryFindAsync(Guid.NewGuid());
        var isNone = true;
        result.Match(() => { }, _ => isNone = false);
        Assert.True(isNone);
    }

    // -----------------------------------------------------------------------
    // AddAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AddAsync_Persists_Email()
    {
        Guid userId;
        await using (var seed = _factory.CreateContext())
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "owner@test.com");

        Guid emailId;
        await using (var ctx = _factory.CreateContext())
        {
            var email = new Email("persisted@test.com");
            var repo = (IRepository<Email, Guid>)ctx;
            await repo.AddAsync(email);
            ctx.Entry(email).Property("UserId").CurrentValue = userId;
            emailId = ctx.Entry(email).Property<Guid>("Id").CurrentValue;
            await ctx.SaveChangesAsync();
        }

        await using var readCtx = _factory.CreateContext();
        var readRepo = (IRepository<Email, Guid>)readCtx;
        var found = false;
        (await readRepo.TryFindAsync(emailId)).Match(() => { }, e => { found = true; Assert.Equal("persisted@test.com", e.Value); });
        Assert.True(found);
    }

    // -----------------------------------------------------------------------
    // GetKey
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetKey_AfterAdd_ReturnsNonEmptyGuid()
    {
        Guid userId;
        await using (var seed = _factory.CreateContext())
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "gk@test.com");

        await using var ctx = _factory.CreateContext();
        var email = new Email("gk2@test.com");
        await ctx.Emails.AddAsync(email);
        ctx.Entry(email).Property("UserId").CurrentValue = userId;

        var repo = (IRepository<Email, Guid>)ctx;
        var key = repo.GetKey(email);
        Assert.NotEqual(Guid.Empty, key);
    }

    // -----------------------------------------------------------------------
    // Delete
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Delete_RemovesEmail_FromDatabase()
    {
        Guid userId;
        await using (var seed = _factory.CreateContext())
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "del_owner@test.com");

        Guid emailId;
        await using (var seed2 = _factory.CreateContext())
            emailId = await DatabaseSeedHelper.SeedEmailAsync(seed2, userId, "del@test.com");

        await using (var ctx = _factory.CreateContext())
        {
            var tracked = await ctx.Emails
                .Where(e => EF.Property<Guid>(e, "Id") == emailId)
                .FirstAsync();
            var repo = (IRepository<Email, Guid>)ctx;
            repo.Delete(tracked);
            await ctx.SaveChangesAsync();
        }

        await using var verify = _factory.CreateContext();
        var isNone = true;
        (await ((IRepository<Email, Guid>)verify).TryFindAsync(emailId))
            .Match(() => { }, _ => isNone = false);
        Assert.True(isNone);
    }
}
