using Behind.Core.WebApi.TwoFactor;
using CredentialCreateOptions = Fido2NetLib.CredentialCreateOptions;

namespace Behind.Tests.CoreWebApi.TwoFactor;

public sealed class TwoFactorEnrollmentTests : IDisposable
{
    private readonly TwoFactorTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private static T Found<T>(Option<T> option) =>
        option.Match(() => throw new InvalidOperationException(), found => found);

    [Fact]
    public async Task BeginTotp_StoresAPendingSecretAndReturnsTheQrCode()
    {
        var user = await _bed.SeedUser();

        var setup = Found(await _bed.Enrollment.BeginTotp(user));

        var uri = new Uri(setup.Uri);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        Assert.Equal(await _bed.PendingSecret(user.Id), setup.Secret);
        Assert.Contains("<svg", setup.QrCode);
        Assert.Equal(setup.Secret, query["secret"]);
        Assert.Equal("sha1", query["algorithm"]);
        Assert.Equal("Behind System", query["issuer"]);
        Assert.EndsWith("ada%40test.com", uri.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        Assert.False((await _bed.Queries.GetStatus(user.Id)).TotpEnabled);
    }

    [Fact]
    public async Task BeginTotp_AgainReplacesThePendingSecret()
    {
        var user = await _bed.SeedUser();

        var first = Found(await _bed.Enrollment.BeginTotp(user));
        var second = Found(await _bed.Enrollment.BeginTotp(user));

        Assert.NotEqual(first.Secret, second.Secret);
        Assert.Equal(second.Secret, await _bed.PendingSecret(user.Id));
    }

    [Fact]
    public async Task BeginTotp_WhenAlreadyEnabled_KeepsTheEnabledSecret()
    {
        var user = await _bed.SeedUser();
        await _bed.EnableTotp(user);
        var secret = await _bed.PendingSecret(user.Id);

        Assert.Equal(None, await _bed.Enrollment.BeginTotp(user));
        Assert.Equal(secret, await _bed.PendingSecret(user.Id));
    }

    [Fact]
    public async Task ConfirmTotp_WithTheRightCodeEnablesItAndIssuesTheFirstRecoveryCodes()
    {
        var user = await _bed.SeedUser();
        var setup = Found(await _bed.Enrollment.BeginTotp(user));

        var enrolled = Found(await _bed.Enrollment.ConfirmTotp(user.Id, _bed.CodeFor(setup.Secret)));

        var status = await _bed.Queries.GetStatus(user.Id);
        Assert.Equal(20, enrolled.RecoveryCodes.Count);
        Assert.True(status.TotpEnabled);
        Assert.Equal(20, status.RecoveryCodesRemaining);
    }

    [Fact]
    public async Task ConfirmTotp_WithAWrongCodeKeepsItPending()
    {
        var user = await _bed.SeedUser();
        await _bed.Enrollment.BeginTotp(user);

        Assert.Equal(None, await _bed.Enrollment.ConfirmTotp(user.Id, "000000"));
        Assert.False((await _bed.Queries.GetStatus(user.Id)).TotpEnabled);
        Assert.Equal(0, (await _bed.Queries.GetStatus(user.Id)).RecoveryCodesRemaining);
    }

    [Fact]
    public async Task ConfirmTotp_WithoutAPendingSecret_IsRejected()
    {
        var user = await _bed.SeedUser();

        Assert.Equal(None, await _bed.Enrollment.ConfirmTotp(user.Id, "123456"));
    }

    [Fact]
    public async Task ConfirmTotp_ASecondMethodKeepsTheExistingRecoveryCodes()
    {
        var user = await _bed.SeedUser();
        using var authenticator = await _bed.EnablePasskey(user);
        var before = (await _bed.Queries.GetRecoveryCodes(user.Id)).Select(code => code.Id).Order().ToList();
        var setup = Found(await _bed.Enrollment.BeginTotp(user));

        var enrolled = Found(await _bed.Enrollment.ConfirmTotp(user.Id, _bed.CodeFor(setup.Secret)));

        Assert.Empty(enrolled.RecoveryCodes);
        Assert.Equal(before, (await _bed.Queries.GetRecoveryCodes(user.Id)).Select(code => code.Id).Order());
    }

    [Fact]
    public async Task BeginPasskey_SealsTheCeremonyInTheTicket()
    {
        var user = await _bed.SeedUser();

        var challenge = await _bed.Enrollment.BeginPasskey(
            TwoFactorTestBed.Origin,
            user,
            TwoFactorStage.Setup,
            MagicLinkFunction.LostPassword);

        var ticket = Found(_bed.Tickets.Open(challenge.Ticket));
        Assert.Equal(TwoFactorStage.Setup, ticket.Stage);
        Assert.Equal(MagicLinkFunction.LostPassword, ticket.Function);
        Assert.Equal(user.Id, ticket.UserId);
        Assert.Equal(CredentialCreateOptions.FromJson(ticket.Ceremony!).Challenge, challenge.Options.Challenge);
        Assert.Equal(user.Id.ToByteArray(), challenge.Options.User.Id);
        Assert.Equal("Ada", challenge.Options.User.DisplayName);
        Assert.Equal("ada@test.com", challenge.Options.User.Name);
    }

    [Fact]
    public async Task BeginPasskey_ExcludesThePasskeysTheUserAlreadyHas()
    {
        var user = await _bed.SeedUser();
        using var authenticator = await _bed.EnablePasskey(user);

        var challenge = await _bed.Enrollment.BeginPasskey(TwoFactorTestBed.Origin, user, TwoFactorStage.Profile, null);

        Assert.Equal(authenticator.CredentialId, Assert.Single(challenge.Options.ExcludeCredentials).Id);
    }

    [Fact]
    public async Task CompletePasskey_StoresTheNamedPasskeyAndIssuesTheFirstRecoveryCodes()
    {
        var user = await _bed.SeedUser();
        using var authenticator = new VirtualAuthenticator();
        var challenge = await _bed.Enrollment.BeginPasskey(TwoFactorTestBed.Origin, user, TwoFactorStage.Profile, null);
        var ticket = Found(_bed.Tickets.Open(challenge.Ticket));

        var enrolled = Found(await _bed.Enrollment.CompletePasskey(
            TwoFactorTestBed.Origin,
            ticket,
            "  YubiKey  ",
            authenticator.Register(challenge.Options, TwoFactorTestBed.OriginText)));

        var passkey = Assert.Single(await _bed.Queries.GetPasskeys(user.Id));
        Assert.Equal(20, enrolled.RecoveryCodes.Count);
        Assert.Equal("YubiKey", passkey.Name);
        Assert.Equal(Base64UrlOf(authenticator.CredentialId), passkey.CredentialId);
    }

    [Fact]
    public async Task CompletePasskey_WithoutANameUsesTheDefaultName()
    {
        var user = await _bed.SeedUser();
        using var authenticator = await _bed.EnablePasskey(user);

        Assert.Equal(TwoFactorEnrollment.DefaultPasskeyName, Assert.Single(await _bed.Queries.GetPasskeys(user.Id)).Name);
    }

    [Fact]
    public async Task CompletePasskey_RejectsAnAuthenticatorThatIsAlreadyRegistered()
    {
        var user = await _bed.SeedUser();
        var other = await _bed.SeedUser("other@test.com");
        using var authenticator = await _bed.EnablePasskey(user);
        var challenge = await _bed.Enrollment.BeginPasskey(TwoFactorTestBed.Origin, other, TwoFactorStage.Profile, null);
        var ticket = Found(_bed.Tickets.Open(challenge.Ticket));

        var enrolled = await _bed.Enrollment.CompletePasskey(
            TwoFactorTestBed.Origin,
            ticket,
            null,
            authenticator.Register(challenge.Options, TwoFactorTestBed.OriginText));

        Assert.Equal(None, enrolled);
        Assert.Empty(await _bed.Queries.GetPasskeys(other.Id));
    }

    [Fact]
    public async Task CompletePasskey_RejectsATicketWithoutACeremony()
    {
        var user = await _bed.SeedUser();
        using var authenticator = new VirtualAuthenticator();
        var challenge = await _bed.Enrollment.BeginPasskey(TwoFactorTestBed.Origin, user, TwoFactorStage.Profile, null);
        var bare = _bed.Tickets.Create(user.Id, TwoFactorStage.Profile);

        Assert.Equal(None, await _bed.Enrollment.CompletePasskey(
            TwoFactorTestBed.Origin,
            bare,
            null,
            authenticator.Register(challenge.Options, TwoFactorTestBed.OriginText)));
    }

    [Fact]
    public async Task RegenerateRecoveryCodes_ReplacesTheCodes()
    {
        var user = await _bed.SeedUser();
        await _bed.EnableTotp(user);
        var before = await _bed.Queries.GetRecoveryCodes(user.Id);

        var codes = await _bed.Enrollment.RegenerateRecoveryCodes(user.Id);

        var after = await _bed.Queries.GetRecoveryCodes(user.Id);
        Assert.Equal(20, codes.Count);
        Assert.Equal(20, after.Count);
        Assert.Empty(before.Select(code => code.Id).Intersect(after.Select(code => code.Id)));
    }

    [Fact]
    public async Task RemoveTotp_WithoutASecret_IsNotFound()
    {
        var user = await _bed.SeedUser();

        Assert.Equal(TwoFactorRemoval.NotFound, await _bed.Enrollment.RemoveTotp(user.Id));
    }

    [Fact]
    public async Task RemoveTotp_TheLastMethodAlsoDropsTheRecoveryCodes()
    {
        var user = await _bed.SeedUser();
        await _bed.EnableTotp(user);

        Assert.Equal(TwoFactorRemoval.Removed, await _bed.Enrollment.RemoveTotp(user.Id));

        var status = await _bed.Queries.GetStatus(user.Id);
        Assert.False(status.IsEnabled);
        Assert.Equal(0, status.RecoveryCodesRemaining);
    }

    [Fact]
    public async Task RemoveTotp_WithAPasskeyLeftKeepsTheRecoveryCodes()
    {
        var user = await _bed.SeedUser();
        using var authenticator = await _bed.EnablePasskey(user);
        await _bed.EnableTotp(user);

        Assert.Equal(TwoFactorRemoval.Removed, await _bed.Enrollment.RemoveTotp(user.Id));

        var status = await _bed.Queries.GetStatus(user.Id);
        Assert.True(status.IsEnabled);
        Assert.Equal(20, status.RecoveryCodesRemaining);
    }

    [Fact]
    public async Task RemoveTotp_PendingSecretIsRemovedWithoutTouchingTheRecoveryCodes()
    {
        var user = await _bed.SeedUser();
        using var authenticator = await _bed.EnablePasskey(user);
        await _bed.Enrollment.BeginTotp(user);

        Assert.Equal(TwoFactorRemoval.Removed, await _bed.Enrollment.RemoveTotp(user.Id));
        Assert.Equal(20, (await _bed.Queries.GetStatus(user.Id)).RecoveryCodesRemaining);
    }

    [Fact]
    public async Task RemoveTotp_TheLastMethodIsRefusedWhenTwoFactorIsRequired()
    {
        using var bed = new TwoFactorTestBed(true);
        var user = await bed.SeedUser();
        await bed.EnableTotp(user);

        Assert.Equal(TwoFactorRemoval.Required, await bed.Enrollment.RemoveTotp(user.Id));
        Assert.True((await bed.Queries.GetStatus(user.Id)).TotpEnabled);
    }

    [Fact]
    public async Task RemoveTotp_IsAllowedWhenTwoFactorIsRequiredButAPasskeyRemains()
    {
        using var bed = new TwoFactorTestBed(true);
        var user = await bed.SeedUser();
        using var authenticator = await bed.EnablePasskey(user);
        await bed.EnableTotp(user);

        Assert.Equal(TwoFactorRemoval.Removed, await bed.Enrollment.RemoveTotp(user.Id));
    }

    [Fact]
    public async Task RemovePasskey_RemovesOnlyAKnownPasskey()
    {
        var user = await _bed.SeedUser();
        using var authenticator = await _bed.EnablePasskey(user);
        await _bed.EnableTotp(user);
        var id = (await _bed.Queries.GetPasskeys(user.Id)).Single().Id;

        Assert.Equal(TwoFactorRemoval.NotFound, await _bed.Enrollment.RemovePasskey(user.Id, Guid.NewGuid()));
        Assert.Equal(TwoFactorRemoval.Removed, await _bed.Enrollment.RemovePasskey(user.Id, id));
        Assert.Empty(await _bed.Queries.GetPasskeys(user.Id));
        Assert.Equal(20, (await _bed.Queries.GetStatus(user.Id)).RecoveryCodesRemaining);
    }

    [Fact]
    public async Task RemovePasskey_TheLastMethodDropsTheRecoveryCodes()
    {
        var user = await _bed.SeedUser();
        using var authenticator = await _bed.EnablePasskey(user);
        var id = (await _bed.Queries.GetPasskeys(user.Id)).Single().Id;

        Assert.Equal(TwoFactorRemoval.Removed, await _bed.Enrollment.RemovePasskey(user.Id, id));
        Assert.Equal(0, (await _bed.Queries.GetStatus(user.Id)).RecoveryCodesRemaining);
    }

    [Fact]
    public async Task RemovePasskey_TheLastMethodIsRefusedWhenTwoFactorIsRequired()
    {
        using var bed = new TwoFactorTestBed(true);
        var user = await bed.SeedUser();
        using var authenticator = await bed.EnablePasskey(user);
        var id = (await bed.Queries.GetPasskeys(user.Id)).Single().Id;

        Assert.Equal(TwoFactorRemoval.Required, await bed.Enrollment.RemovePasskey(user.Id, id));
        Assert.Single(await bed.Queries.GetPasskeys(user.Id));
    }

    private static string Base64UrlOf(byte[] value) => System.Buffers.Text.Base64Url.EncodeToString(value);
}
