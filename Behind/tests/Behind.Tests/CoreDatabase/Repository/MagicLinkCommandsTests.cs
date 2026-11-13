using Behind.Core.Database.Aggregates;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase.Repository;

public sealed class MagicLinkCommandsTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<Guid> SeedUserAsync(string email)
    {
        await using var seed = _factory.CreateContext();
        return await DatabaseSeedHelper.SeedHumanUserAsync(seed, email);
    }

    private async Task<MagicLink> IssueAsync(
        Guid userId,
        TimeSpan lifetime,
        MagicLinkFunction function = MagicLinkFunction.SignIn)
    {
        var link = MagicLink.Create(Now, lifetime, function);
        await using var ctx = _factory.CreateContext();
        await new MagicLinkCommands(ctx).Issue(userId, link);
        return link;
    }

    private async Task<Option<MagicLinkRedemption>> ConsumeAsync(TokenNonce nonce, DateTimeOffset at)
    {
        await using var ctx = _factory.CreateContext();
        return await new MagicLinkCommands(ctx).Consume(nonce, at);
    }

    [Fact]
    public async Task Issue_PersistsLinkForUser()
    {
        var userId = await SeedUserAsync("issue@test.com");

        var link = await IssueAsync(userId, TimeSpan.FromMinutes(15));

        await using var reader = _factory.CreateContext();
        Assert.Equal(link, await reader.MagicLinks.AsNoTracking().SingleAsync());
        Assert.Equal(userId, await reader.MagicLinks.Select(l => EF.Property<Guid>(l, "UserId")).SingleAsync());
    }

    [Fact]
    public async Task Consume_ValidLink_ReturnsUserIdAndMarksConsumed()
    {
        var userId = await SeedUserAsync("consume@test.com");
        var link = await IssueAsync(userId, TimeSpan.FromMinutes(15));

        var result = await ConsumeAsync(link.Nonce, Now.AddMinutes(1));

        Assert.Equal(Some(new MagicLinkRedemption(userId, MagicLinkFunction.SignIn)), result);
        await using var reader = _factory.CreateContext();
        Assert.Equal(
            Now.AddMinutes(1),
            await reader.MagicLinks
                .Select(l => EF.Property<DateTimeOffset?>(l, MagicLinkConfiguration.ConsumedAtProperty))
                .SingleAsync());
    }

    [Theory]
    [InlineData(MagicLinkFunction.SignIn)]
    [InlineData(MagicLinkFunction.LostPassword)]
    public async Task Issue_ThenConsume_RoundTripsTheFunction(MagicLinkFunction function)
    {
        var userId = await SeedUserAsync($"{function}@test.com");
        var link = await IssueAsync(userId, TimeSpan.FromMinutes(15), function);

        await using var reader = _factory.CreateContext();
        Assert.Equal(function, await reader.MagicLinks.AsNoTracking().Select(l => l.Function).SingleAsync());
        Assert.Equal(
            Some(new MagicLinkRedemption(userId, function)),
            await ConsumeAsync(link.Nonce, Now.AddMinutes(1)));
    }

    [Fact]
    public async Task Consume_SecondTime_ReturnsNone()
    {
        var userId = await SeedUserAsync("twice@test.com");
        var link = await IssueAsync(userId, TimeSpan.FromMinutes(15));

        await ConsumeAsync(link.Nonce, Now.AddMinutes(1));

        Assert.Equal(None, await ConsumeAsync(link.Nonce, Now.AddMinutes(2)));
    }

    [Fact]
    public async Task Consume_ExpiredLink_ReturnsNone()
    {
        var userId = await SeedUserAsync("expired@test.com");
        var link = await IssueAsync(userId, TimeSpan.FromMinutes(15));

        Assert.Equal(None, await ConsumeAsync(link.Nonce, Now.AddMinutes(15)));
    }

    [Fact]
    public async Task Consume_UnknownNonce_ReturnsNone() =>
        Assert.Equal(None, await ConsumeAsync(Guid.NewGuid(), Now));

    [Fact]
    public async Task DeleteExpired_RemovesOnlyExpiredLinks()
    {
        var userId = await SeedUserAsync("cleanup@test.com");
        var expired = await IssueAsync(userId, TimeSpan.FromMinutes(5));
        var active = await IssueAsync(userId, TimeSpan.FromHours(1));

        await using var ctx = _factory.CreateContext();
        var deleted = await new MagicLinkCommands(ctx).DeleteExpired(Now.AddMinutes(10));

        Assert.Equal(1, deleted);
        await using var reader = _factory.CreateContext();
        var remaining = await reader.MagicLinks.AsNoTracking().ToListAsync();
        Assert.Equal([active], remaining);
        Assert.DoesNotContain(expired, remaining);
    }

    [Fact]
    public async Task HasIssuedSince_ReflectsRecentIssuesForTheUserOnly()
    {
        var userId = await SeedUserAsync("recent@test.com");
        var otherId = await SeedUserAsync("other@test.com");
        await IssueAsync(userId, TimeSpan.FromMinutes(15));

        await using var ctx = _factory.CreateContext();
        var queries = new MagicLinkQueries(ctx);

        Assert.True(await queries.HasIssuedSince(userId, Now.AddMinutes(-1)));
        Assert.False(await queries.HasIssuedSince(userId, Now));
        Assert.False(await queries.HasIssuedSince(otherId, Now.AddMinutes(-1)));
    }

    [Fact]
    public async Task Consume_IgnoresPasswordResetGrants()
    {
        var userId = await SeedUserAsync("grant-consume@test.com");
        var grant = await IssueAsync(userId, TimeSpan.FromMinutes(10), MagicLinkFunction.PasswordReset);

        Assert.Equal(None, await ConsumeAsync(grant.Nonce, Now.AddMinutes(1)));

        await using var ctx = _factory.CreateContext();
        Assert.Equal(Some(userId), await new MagicLinkCommands(ctx).ConsumePasswordReset(grant.Nonce, Now.AddMinutes(1)));
    }

    [Theory]
    [InlineData(MagicLinkFunction.SignIn)]
    [InlineData(MagicLinkFunction.LostPassword)]
    public async Task ConsumePasswordReset_IgnoresEmailedLinks(MagicLinkFunction function)
    {
        var userId = await SeedUserAsync("link-reset@test.com");
        var link = await IssueAsync(userId, TimeSpan.FromMinutes(15), function);

        await using var ctx = _factory.CreateContext();
        Assert.Equal(None, await new MagicLinkCommands(ctx).ConsumePasswordReset(link.Nonce, Now.AddMinutes(1)));
        Assert.Equal(None, await new MagicLinkQueries(ctx).FindPasswordResetUser(link.Nonce, Now.AddMinutes(1)));
    }

    [Fact]
    public async Task ConsumePasswordReset_IsSingleUseAndExpires()
    {
        var userId = await SeedUserAsync("grant-once@test.com");
        var used = await IssueAsync(userId, TimeSpan.FromMinutes(10), MagicLinkFunction.PasswordReset);
        var expired = await IssueAsync(userId, TimeSpan.FromMinutes(10), MagicLinkFunction.PasswordReset);

        await using var ctx = _factory.CreateContext();
        var commands = new MagicLinkCommands(ctx);
        var queries = new MagicLinkQueries(ctx);

        Assert.Equal(Some(userId), await queries.FindPasswordResetUser(used.Nonce, Now.AddMinutes(1)));
        Assert.Equal(Some(userId), await commands.ConsumePasswordReset(used.Nonce, Now.AddMinutes(1)));
        Assert.Equal(None, await commands.ConsumePasswordReset(used.Nonce, Now.AddMinutes(2)));
        Assert.Equal(None, await queries.FindPasswordResetUser(used.Nonce, Now.AddMinutes(2)));
        Assert.Equal(None, await queries.FindPasswordResetUser(expired.Nonce, Now.AddMinutes(10)));
        Assert.Equal(None, await commands.ConsumePasswordReset(expired.Nonce, Now.AddMinutes(10)));
    }

    [Fact]
    public async Task RevokeForUser_BurnsEveryOutstandingLinkAndGrantOfTheUserOnly()
    {
        var userId = await SeedUserAsync("revoke@test.com");
        var otherId = await SeedUserAsync("revoke-other@test.com");
        var signIn = await IssueAsync(userId, TimeSpan.FromMinutes(15));
        var lostPassword = await IssueAsync(userId, TimeSpan.FromMinutes(15), MagicLinkFunction.LostPassword);
        var grant = await IssueAsync(userId, TimeSpan.FromMinutes(10), MagicLinkFunction.PasswordReset);
        var others = await IssueAsync(otherId, TimeSpan.FromMinutes(15));

        await using (var ctx = _factory.CreateContext())
        {
            Assert.Equal(3, await new MagicLinkCommands(ctx).RevokeForUser(userId, Now.AddMinutes(1)));
        }

        Assert.Equal(None, await ConsumeAsync(signIn.Nonce, Now.AddMinutes(2)));
        Assert.Equal(None, await ConsumeAsync(lostPassword.Nonce, Now.AddMinutes(2)));
        await using (var ctx = _factory.CreateContext())
        {
            Assert.Equal(None, await new MagicLinkCommands(ctx).ConsumePasswordReset(grant.Nonce, Now.AddMinutes(2)));
        }

        Assert.Equal(Some(new MagicLinkRedemption(otherId, MagicLinkFunction.SignIn)), await ConsumeAsync(others.Nonce, Now.AddMinutes(2)));
    }

    [Fact]
    public async Task HasIssuedSince_IgnoresPasswordResetGrants()
    {
        var userId = await SeedUserAsync("grant-cooldown@test.com");
        await IssueAsync(userId, TimeSpan.FromMinutes(10), MagicLinkFunction.PasswordReset);

        await using var ctx = _factory.CreateContext();

        Assert.False(await new MagicLinkQueries(ctx).HasIssuedSince(userId, Now.AddMinutes(-1)));
    }
}
