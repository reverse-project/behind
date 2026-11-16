using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.Domain;
using Behind.Tests.Helpers;

namespace Behind.Tests.CoreDatabase;

public sealed class ReportBanTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task TemporaryBanExpiresAtTheExactConfiguredBoundary()
    {
        await using var context = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(context, "reported@example.test");
        var moderatorId = await DatabaseSeedHelper.SeedHumanUserAsync(context, "moderator@example.test");
        var expires = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
        await new WallOfShameCommands(context).BanUntil(userId, moderatorId, new Reason("Misconduct"), expires);
        var queries = new WallOfShameQueries(context);
        Assert.True((await queries.FindActiveBan(userId, expires.AddTicks(-1))).Match(() => false, _ => true));
        Assert.False((await queries.FindActiveBan(userId, expires)).Match(() => false, _ => true));
    }

    [Fact]
    public async Task PermanentBanRemainsActive()
    {
        await using var context = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(context, "reported@example.test");
        var moderatorId = await DatabaseSeedHelper.SeedHumanUserAsync(context, "moderator@example.test");
        await new WallOfShameCommands(context).BanUntil(userId, moderatorId, new Reason("Misconduct"), null);
        Assert.True((await new WallOfShameQueries(context).FindActiveBan(userId,
            new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero))).Match(() => false, _ => true));
    }
}
