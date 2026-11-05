using Behind.Core.Database;
using Behind.Core.WebApi.TwoFactor;

namespace Behind.Tests.CoreWebApi.TwoFactor;

public sealed class TwoFactorGateTests : IDisposable
{
    private readonly TwoFactorTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private static TwoFactorTicket TicketOf(TwoFactorTestBed bed, Option<TwoFactorChallengeResponse> challenge) =>
        bed.Tickets.Open(challenge.Match(() => throw new InvalidOperationException(), found => found.Ticket))
            .Match(() => throw new InvalidOperationException(), found => found);

    [Fact]
    public async Task Challenge_UserWithoutTwoFactorWhenOptional_RequiresNothing()
    {
        var user = await _bed.SeedUser();

        Assert.Equal(None, await _bed.Gate.Challenge(user.Id, null));
    }

    [Fact]
    public async Task Challenge_UserWithoutTwoFactorWhenRequired_AsksForASetup()
    {
        using var bed = new TwoFactorTestBed(true);
        var user = await bed.SeedUser();

        var challenge = await bed.Gate.Challenge(user.Id, MagicLinkFunction.LostPassword);

        var response = challenge.Match(() => throw new InvalidOperationException(), found => found);
        var ticket = TicketOf(bed, challenge);
        Assert.True(response.SetupRequired);
        Assert.Equal([TwoFactorMethod.Totp, TwoFactorMethod.Passkey], response.Methods);
        Assert.Equal(MagicLinkFunction.LostPassword, response.Function);
        Assert.Equal(TwoFactorStage.Setup, ticket.Stage);
        Assert.Equal(user.Id, ticket.UserId);
        Assert.Equal(MagicLinkFunction.LostPassword, ticket.Function);
    }

    [Fact]
    public async Task Challenge_UserWithAnAuthenticatorApp_AsksForAVerification()
    {
        var user = await _bed.SeedUser();
        await _bed.EnableTotp(user);

        var challenge = await _bed.Gate.Challenge(user.Id, null);

        var response = challenge.Match(() => throw new InvalidOperationException(), found => found);
        Assert.False(response.SetupRequired);
        Assert.Equal([TwoFactorMethod.Totp, TwoFactorMethod.Recovery], response.Methods);
        Assert.Equal(TwoFactorStage.Verify, TicketOf(_bed, challenge).Stage);
    }

    [Fact]
    public async Task Challenge_UserWithOnlyAPasskey_OffersThePasskeyAndRecovery()
    {
        var user = await _bed.SeedUser();
        using var authenticator = await _bed.EnablePasskey(user);

        var challenge = await _bed.Gate.Challenge(user.Id, null);

        Assert.Equal(
            [TwoFactorMethod.Passkey, TwoFactorMethod.Recovery],
            challenge.Match(() => throw new InvalidOperationException(), found => found.Methods));
    }

    [Fact]
    public async Task Challenge_EnabledUserWhenRequired_StillAsksForAVerification()
    {
        using var bed = new TwoFactorTestBed(true);
        var user = await bed.SeedUser();
        await bed.EnableTotp(user);

        var challenge = await bed.Gate.Challenge(user.Id, null);

        Assert.False(challenge.Match(() => true, found => found.SetupRequired));
        Assert.Equal(TwoFactorStage.Verify, TicketOf(bed, challenge).Stage);
    }

    [Fact]
    public async Task Challenge_PendingAuthenticatorApp_DoesNotCountAsEnabled()
    {
        var user = await _bed.SeedUser();
        await _bed.Enrollment.BeginTotp(user);

        Assert.Equal(None, await _bed.Gate.Challenge(user.Id, null));
    }

    [Fact]
    public void Methods_OmitRecoveryWhenNoCodesRemain()
    {
        var status = new ITwoFactorQueries.TwoFactorStatus(true, [], 0);

        Assert.Equal([TwoFactorMethod.Totp], status.Methods);
    }
}
