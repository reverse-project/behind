using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Queries;
using Behind.Tests.Helpers;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class AuditLogEntryQueriesDeletedUserTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetAuditLog_EntryByDeletedUser_StillResolvesTheUser()
    {
        Guid userId;
        await using (var seed = _factory.CreateContext())
        {
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "gone@test.com");
            await DatabaseSeedHelper.SeedAuditLogEntryAsync(seed, userId);
        }

        await using (var seed = _factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            await seed.GetRepository<HumanUser, Guid>().DeleteAsync(userId);
            await seed.SaveChangesAsync();
        }

        await using var ctx = _factory.CreateContext();
        var result = await new AuditLogEntryQueries(ctx).GetAuditLog(
            new IAuditLogEntryQueries.RequestAuditLog(
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                new EmptyFilter<AuditLogEntry>(),
                new Flipper(1, 20),
                new OrderByCollection()));

        var user = result.Match(
            () => throw new InvalidOperationException("Expected Some"),
            paged => paged.Results.Match(
                () => throw new InvalidOperationException("Expected results"),
                items => items.Single(item => item.EntityName == "Entity").User));

        var human = Assert.IsType<HumanUser>(user);
        Assert.Equal("Test User", human.DisplayName);
        Assert.Equal("gone@test.com", Assert.Single(human.Emails).Value);
        Assert.False(ctx.SoftDelete.IncludesDeleted);
    }
}
