using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.TwoFactor;

namespace Behind.Tests.CoreWebApi.TwoFactor;

public sealed class TwoFactorGateTests : IDisposable
{
    private readonly TwoFactorTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private static TwoFactorTicket TicketOf(TwoFactorTestBed bed, Option<TwoFactorChallengeResponse> challenge) =>
        bed.Tickets.Open(challenge.Match(() => throw new InvalidOperationException(), found => found.Ticket))
            .Match(() => throw new InvalidOperationException(), found => found);

    private static async Task ConfirmEmail(TwoFactorTestBed bed, Guid userId)
    {
        var address = Assert.Single(await new EmailQueries(bed.Context).Addresses(userId));
        Assert.True(await new EmailCommands(bed.Context, bed.Time)
            .ConfirmAddress(userId, address.Id, address.Value));
    }

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
        await ConfirmEmail(bed, user.Id);

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
        await ConfirmEmail(_bed, user.Id);

        var challenge = await _bed.Gate.Challenge(user.Id, null);

        var response = challenge.Match(() => throw new InvalidOperationException(), found => found);
        Assert.False(response.SetupRequired);
        Assert.Equal([TwoFactorMethod.Totp, TwoFactorMethod.Recovery, TwoFactorMethod.Email], response.Methods);
        Assert.Equal(TwoFactorStage.Verify, TicketOf(_bed, challenge).Stage);
    }

    [Fact]
    public async Task Challenge_UserWithOnlyAPasskey_OffersThePasskeyAndRecovery()
    {
        var user = await _bed.SeedUser();
        using var authenticator = await _bed.EnablePasskey(user);
        await ConfirmEmail(_bed, user.Id);

        var challenge = await _bed.Gate.Challenge(user.Id, null);

        Assert.Equal(
            [TwoFactorMethod.Passkey, TwoFactorMethod.Recovery, TwoFactorMethod.Email],
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

    [Fact]
    public async Task Challenge_DisabledTwoFactorDoesNotBypassEnrolledUsers()
    {
        using var bed = new TwoFactorTestBed(accounts: new AccountsConfiguration { TwoFactor = false });
        var user = await bed.SeedUser();
        await bed.EnableTotp(user);

        var challenge = await bed.Gate.Challenge(user.Id, null);

        Assert.True(challenge.Match(() => false, found => found.Unavailable));
    }

    [Fact]
    public async Task Challenge_DisabledTwoFactorBlocksRequiredSetup()
    {
        using var bed = new TwoFactorTestBed(true, new AccountsConfiguration { TwoFactor = false });
        var user = await bed.SeedUser();

        Assert.True((await bed.Gate.Challenge(user.Id, null)).Match(() => false, found => found.Unavailable));
    }

    [Fact]
    public async Task Challenge_DisabledTwoFactorAllowsUnenrolledUsersWhenOptional()
    {
        using var bed = new TwoFactorTestBed(accounts: new AccountsConfiguration { TwoFactor = false });
        var user = await bed.SeedUser();

        Assert.Equal(None, await bed.Gate.Challenge(user.Id, null));
    }

    [Fact]
    public async Task Challenge_DisabledPasskeyRetainsRecovery()
    {
        using var bed = new TwoFactorTestBed(accounts: new AccountsConfiguration { Passkey = false });
        var user = await bed.SeedUser();
        using var authenticator = await bed.EnablePasskey(user);
        await ConfirmEmail(bed, user.Id);

        var challenge = await bed.Gate.Challenge(user.Id, null);
        Assert.Equal([TwoFactorMethod.Recovery, TwoFactorMethod.Email],
            challenge.Match(() => throw new InvalidOperationException(), found => found.Methods));
    }

    [Fact]
    public async Task PasskeyPrimary_ExcludesTheSamePasskeyButAllowsAnother()
    {
        var user = await _bed.SeedUser();
        using var first = await _bed.EnablePasskey(user);
        using var second = await _bed.EnablePasskey(user);
        var primary = System.Buffers.Text.Base64Url.EncodeToString(first.CredentialId);

        var result = await _bed.Gate.ChallengePasskey(user.Id, primary);
        var challenge = result.Match(() => throw new InvalidOperationException(), found => found);
        Assert.Contains(TwoFactorMethod.Passkey, challenge.Methods);
        var ticket = TicketOf(_bed, result);
        Assert.Equal(primary, ticket.PrimaryCredentialId);
        Assert.NotNull(ticket.Nonce);
    }

    [Fact]
    public async Task PasskeyPrimary_OnlyPasskeyCannotBeReusedAsSecondFactor()
    {
        var user = await _bed.SeedUser();
        using var first = await _bed.EnablePasskey(user);

        var result = await _bed.Gate.ChallengePasskey(user.Id,
            System.Buffers.Text.Base64Url.EncodeToString(first.CredentialId));
        var challenge = result.Match(() => throw new InvalidOperationException(), found => found);

        Assert.DoesNotContain(TwoFactorMethod.Passkey, challenge.Methods);
        Assert.Contains(TwoFactorMethod.Email, challenge.Methods);
        Assert.Contains(TwoFactorMethod.Recovery, challenge.Methods);
    }

    [Fact]
    public async Task Challenge_DisabledPasskeyIsNotOfferedDuringRequiredSetup()
    {
        using var bed = new TwoFactorTestBed(true, new AccountsConfiguration { Passkey = false });
        var user = await bed.SeedUser();
        await ConfirmEmail(bed, user.Id);

        var challenge = await bed.Gate.Challenge(user.Id, null);
        Assert.Equal([TwoFactorMethod.Totp, TwoFactorMethod.Email],
            challenge.Match(() => throw new InvalidOperationException(), found => found.Methods));
    }

    [Theory]
    [InlineData(MagicLinkFunction.SignIn, true)]
    [InlineData(MagicLinkFunction.LostPassword, false)]
    public async Task Challenge_DisabledSignInLinksPreservePasswordRecovery(MagicLinkFunction function, bool unavailable)
    {
        using var bed = new TwoFactorTestBed(accounts: new AccountsConfiguration { MagicLink = false });
        var user = await bed.SeedUser();

        Assert.Equal(unavailable, (await bed.Gate.Challenge(user.Id, function)).Match(() => false, found => found.Unavailable));
    }

    [Theory]
    [InlineData(MagicLinkFunction.SignIn)]
    [InlineData(MagicLinkFunction.LostPassword)]
    [InlineData(MagicLinkFunction.PasswordReset)]
    public async Task Challenge_NeverOffersAnEmailedCodeAfterAnEmailedLink(MagicLinkFunction function)
    {
        var user = await _bed.SeedUser();
        await _bed.EnableTotp(user);
        await ConfirmEmail(_bed, user.Id);
        using var required = new TwoFactorTestBed(true);
        var unenrolled = await required.SeedUser();
        await ConfirmEmail(required, unenrolled.Id);

        var verify = await _bed.Gate.Challenge(user.Id, function);
        var setup = await required.Gate.Challenge(unenrolled.Id, function);

        Assert.Equal([TwoFactorMethod.Totp, TwoFactorMethod.Recovery],
            verify.Match(() => throw new InvalidOperationException(), found => found.Methods));
        Assert.Equal([TwoFactorMethod.Totp, TwoFactorMethod.Passkey],
            setup.Match(() => throw new InvalidOperationException(), found => found.Methods));
    }

    [Fact]
    public async Task Challenge_OffersAnEmailedCodeDuringARequiredSetupAfterAPassword()
    {
        using var bed = new TwoFactorTestBed(true);
        var user = await bed.SeedUser();
        await ConfirmEmail(bed, user.Id);

        var challenge = await bed.Gate.Challenge(user.Id, null);

        Assert.Equal([TwoFactorMethod.Totp, TwoFactorMethod.Passkey, TwoFactorMethod.Email],
            challenge.Match(() => throw new InvalidOperationException(), found => found.Methods));
    }

    [Fact]
    public async Task Challenge_EmailedCodeDoesNotChallengeUnprotectedUsers()
    {
        var user = await _bed.SeedUser();

        Assert.Equal(None, await _bed.Gate.Challenge(user.Id, null));
    }

    [Fact]
    public async Task Challenge_DisabledEmailTwoFactorIsNotOffered()
    {
        using var bed = new TwoFactorTestBed(accounts: new AccountsConfiguration { EmailTwoFactor = false });
        var user = await bed.SeedUser();
        await bed.EnableTotp(user);
        await ConfirmEmail(bed, user.Id);

        var challenge = await bed.Gate.Challenge(user.Id, null);

        Assert.Equal([TwoFactorMethod.Totp, TwoFactorMethod.Recovery],
            challenge.Match(() => throw new InvalidOperationException(), found => found.Methods));
    }
}
