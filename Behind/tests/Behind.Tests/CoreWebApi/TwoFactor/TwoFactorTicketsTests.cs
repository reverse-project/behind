using Behind.Core.WebApi.TwoFactor;
using Microsoft.AspNetCore.DataProtection;

namespace Behind.Tests.CoreWebApi.TwoFactor;

public class TwoFactorTicketsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly AdjustableTimeProvider _time = new(Now);
    private readonly TwoFactorTickets _tickets;

    public TwoFactorTicketsTests()
    {
        _tickets = new TwoFactorTickets(DataProtectionProvider.Create("tickets"), _time);
    }

    [Fact]
    public void Open_ReturnsTheSealedTicket()
    {
        var userId = Guid.NewGuid();
        var ticket = _tickets.Create(userId, TwoFactorStage.Setup, MagicLinkFunction.LostPassword, "ceremony");

        var opened = _tickets.Open(_tickets.Seal(ticket));

        Assert.Equal(Some(ticket), opened);
    }

    [Fact]
    public void Create_ExpiresAfterTheLifetime()
    {
        var ticket = _tickets.Create(Guid.NewGuid(), TwoFactorStage.Verify);

        Assert.Equal(Now + TwoFactorTickets.Lifetime, ticket.Expires);
        Assert.Null(ticket.Function);
        Assert.Null(ticket.Ceremony);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-ticket")]
    public void Open_RejectsMissingOrMalformedTokens(string? token)
    {
        Assert.Equal(None, _tickets.Open(token));
    }

    [Fact]
    public void Open_RejectsATamperedToken()
    {
        var token = _tickets.Seal(_tickets.Create(Guid.NewGuid(), TwoFactorStage.Verify));
        var tampered = token[..^4] + (token.EndsWith("AAAA") ? "BBBB" : "AAAA");

        Assert.Equal(None, _tickets.Open(tampered));
    }

    [Fact]
    public void Open_RejectsAnExpiredTicket()
    {
        var token = _tickets.Seal(_tickets.Create(Guid.NewGuid(), TwoFactorStage.Verify));

        _time.Advance(TwoFactorTickets.Lifetime + TimeSpan.FromSeconds(1));

        Assert.Equal(None, _tickets.Open(token));
    }

    [Fact]
    public void Open_AcceptsATicketUntilItExpires()
    {
        var token = _tickets.Seal(_tickets.Create(Guid.NewGuid(), TwoFactorStage.Verify));

        _time.Advance(TwoFactorTickets.Lifetime - TimeSpan.FromSeconds(1));

        Assert.NotEqual(None, _tickets.Open(token));
    }

    [Fact]
    public void Open_RejectsATicketSealedByAnotherApplication()
    {
        var other = new TwoFactorTickets(DataProtectionProvider.Create("another"), _time);
        var token = other.Seal(other.Create(Guid.NewGuid(), TwoFactorStage.Verify));

        Assert.Equal(None, _tickets.Open(token));
    }
}
