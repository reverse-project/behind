using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.Database.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase.Repository;

public sealed class MagicLinkNonceTests : IDisposable
{
    private static readonly DateTimeOffset Now = TwoFactorTestBed.Start;
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData(MagicLinkFunction.EmailTwoFactor)]
    [InlineData(MagicLinkFunction.PasskeyChallenge)]
    [InlineData(MagicLinkFunction.TwoFactorChallenge)]
    public async Task DedicatedNonces_CannotBeConsumedAsMagicLinks(MagicLinkFunction function)
    {
        await using var context = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(context, "nonce@test.com");
        var link = MagicLink.Create(Now, TimeSpan.FromMinutes(3), function);
        var commands = new MagicLinkCommands(context);
        await commands.Issue(userId, link);

        Assert.Equal(None, await commands.Consume(link.Nonce, Now));
        Assert.False(await commands.ConsumeForFunction(link.Nonce, userId, MagicLinkFunction.SignIn, Now));
        Assert.True(await commands.ConsumeForFunction(link.Nonce, userId, function, Now));
        Assert.False(await commands.ConsumeForFunction(link.Nonce, userId, function, Now));
    }

    [Fact]
    public async Task ConsumeForFunction_BindsUserAndExpiry()
    {
        await using var context = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(context, "bound@test.com");
        var link = MagicLink.Create(Now, TimeSpan.FromMinutes(3), MagicLinkFunction.PasskeyChallenge);
        var commands = new MagicLinkCommands(context);
        await commands.Issue(userId, link);

        Assert.False(await commands.ConsumeForFunction(link.Nonce, Guid.NewGuid(), link.Function, Now));
        Assert.False(await commands.ConsumeForFunction(link.Nonce, userId, link.Function, Now.AddSeconds(-1)));
        Assert.False(await commands.ConsumeForFunction(link.Nonce, userId, link.Function, link.ExpiresAt));
        Assert.True(await new MagicLinkQueries(context).IsUsableForFunction(link.Nonce, userId, link.Function, Now));
        Assert.True(await commands.ConsumeForFunction(link.Nonce, userId, link.Function, Now));
    }

    [Fact]
    public async Task TryIssue_ReservesNonceOnceAcrossContexts()
    {
        Guid userId;
        var link = MagicLink.Create(Now, TimeSpan.FromMinutes(3), MagicLinkFunction.EmailTwoFactor);
        await using (var first = _factory.CreateContext())
        {
            userId = await DatabaseSeedHelper.SeedHumanUserAsync(first, "slot@test.com");
            Assert.True(await new MagicLinkCommands(first).TryIssue(userId, link));
        }

        await using var second = _factory.CreateContext();
        Assert.False(await new MagicLinkCommands(second).TryIssue(userId, link));
        Assert.Single(await second.MagicLinks.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task ConsumeForFunction_IsSingleUseAcrossConcurrentConnectionsAndDatabaseReopening()
    {
        var path = $"magic-link-nonce-{Guid.NewGuid():N}.db";
        var options = new DbContextOptionsBuilder<CoreContextSqlite>()
            .UseSqlite($"Data Source={path};Pooling=False")
            .Options;
        var link = MagicLink.Create(Now, TimeSpan.FromMinutes(3), MagicLinkFunction.PasskeyChallenge);

        try
        {
            Guid userId;
            await using (var context = new CoreContextSqlite(options))
            {
                await context.Database.EnsureCreatedAsync();
                userId = await DatabaseSeedHelper.SeedHumanUserAsync(context, "concurrent@test.com");
                await new MagicLinkCommands(context).Issue(userId, link);
            }

            async Task<bool> Consume()
            {
                await using var context = new CoreContextSqlite(options);
                return await new MagicLinkCommands(context).ConsumeForFunction(link.Nonce, userId, link.Function, Now);
            }

            var results = await Task.WhenAll(Task.Run(Consume), Task.Run(Consume));
            Assert.Single(results, consumed => consumed);
            Assert.False(await Consume());

            var reservation = MagicLink.Create(Now, TimeSpan.FromMinutes(1), MagicLinkFunction.EmailTwoFactor);
            async Task<bool> Reserve()
            {
                await using var context = new CoreContextSqlite(options);
                return await new MagicLinkCommands(context).TryIssue(userId, reservation);
            }

            var reservations = await Task.WhenAll(Task.Run(Reserve), Task.Run(Reserve));
            Assert.Single(reservations, reserved => reserved);
            Assert.False(await Reserve());

            var challenge = MagicLink.Create(Now, TimeSpan.FromMinutes(3), MagicLinkFunction.TwoFactorChallenge);
            await using var first = new CoreContextSqlite(options);
            var firstCommands = new MagicLinkCommands(first);
            await firstCommands.Issue(userId, challenge);
            await using var transaction = await first.Database.BeginTransactionAsync();
            Assert.True(await firstCommands.LockForFunction(challenge.Nonce, userId, challenge.Function, Now));
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var competing = Task.Run(async () =>
            {
                await using var other = new CoreContextSqlite(options);
                started.SetResult();
                await using var otherTransaction = await other.Database.BeginTransactionAsync();
                var locked = await new MagicLinkCommands(other).LockForFunction(challenge.Nonce, userId, challenge.Function, Now);
                await otherTransaction.CommitAsync();
                return locked;
            });
            await started.Task;
            Assert.True(await firstCommands.ConsumeForFunction(challenge.Nonce, userId, challenge.Function, Now));
            await transaction.CommitAsync();
            Assert.False(await competing);
        }
        finally
        {
            File.Delete(path);
            File.Delete($"{path}-wal");
            File.Delete($"{path}-shm");
        }
    }
}
