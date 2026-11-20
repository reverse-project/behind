using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.TwoFactor;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class TwoFactorSetupRevocationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SetupTotpCannotContinueAfterAnotherChallengeEnrolledAPasskey(bool confirm)
    {
        using var factory = new TestDbContextFactory();
        await using var database = factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(database);
        await new TwoFactorCommands(database).AddPasskey(userId, new UserPasskey("Owner", "credential", "public-key"));
        var tickets = Substitute.For<ITwoFactorTickets>();
        tickets.Open("stale-setup").Returns(Some(new TwoFactorTicket(userId, TwoFactorStage.Setup,
            DateTimeOffset.UtcNow.AddMinutes(1), null, null) { Nonce = Guid.NewGuid() }));
        var commands = Substitute.For<IMagicLinkCommands>();
        commands.LockForFunction(Arg.Any<TokenNonce>(), userId, MagicLinkFunction.TwoFactorChallenge,
            Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(true);
        var filter = new TwoFactorChallengeFilter(database, commands, tickets, TimeProvider.System,
            NullLogger<TwoFactorChallengeFilter>.Instance);
        object request = confirm
            ? new TwoFactorSignInEndpoints.CodeRequest("stale-setup", "123456")
            : new TwoFactorSignInEndpoints.TicketRequest("stale-setup");
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = confirm
            ? "/sign/in/two-factor/setup/totp/confirm"
            : "/sign/in/two-factor/setup/totp";
        var context = new DefaultEndpointFilterInvocationContext(httpContext, request);
        var called = false;
        var result = await filter.InvokeAsync(context, _ =>
        {
            called = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        });
        Assert.False(called);
        Assert.Equal(409, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }
}
