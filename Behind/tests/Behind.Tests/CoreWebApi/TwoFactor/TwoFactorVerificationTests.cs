using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.TwoFactor;

namespace Behind.Tests.CoreWebApi.TwoFactor;

public sealed class TwoFactorVerificationTests : IDisposable
{
    private readonly TwoFactorTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task<(ApplicationUser User, string Secret)> UserWithTotp()
    {
        var user = await _bed.SeedUser();
        await _bed.EnableTotp(user);

        return (user, await _bed.PendingSecret(user.Id));
    }

    [Fact]
    public async Task VerifyTotp_AcceptsTheCurrentCode()
    {
        var (user, secret) = await UserWithTotp();

        Assert.True(await _bed.Verification.VerifyTotp(user.Id, _bed.CodeFor(secret)));
    }

    [Fact]
    public async Task VerifyTotp_IgnoresSpacesInTheCode()
    {
        var (user, secret) = await UserWithTotp();
        var code = _bed.CodeFor(secret);

        Assert.True(await _bed.Verification.VerifyTotp(user.Id, $"{code[..3]} {code[3..]}"));
    }

    [Fact]
    public async Task VerifyTotp_RejectsAReplayedCode()
    {
        var (user, secret) = await UserWithTotp();
        var code = _bed.CodeFor(secret);

        Assert.True(await _bed.Verification.VerifyTotp(user.Id, code));
        Assert.False(await _bed.Verification.VerifyTotp(user.Id, code));
    }

    [Fact]
    public async Task VerifyTotp_AcceptsTheNextStepAfterAReplayedOne()
    {
        var (user, secret) = await UserWithTotp();

        Assert.True(await _bed.Verification.VerifyTotp(user.Id, _bed.CodeFor(secret)));

        _bed.Time.Advance(TimeSpan.FromSeconds(30));

        Assert.True(await _bed.Verification.VerifyTotp(user.Id, _bed.CodeFor(secret)));
    }

    [Fact]
    public async Task VerifyTotp_ToleratesOneStepOfClockDrift()
    {
        var (user, secret) = await UserWithTotp();

        Assert.True(await _bed.Verification.VerifyTotp(user.Id, _bed.CodeFor(secret, TimeSpan.FromSeconds(30))));
    }

    [Fact]
    public async Task VerifyTotp_RejectsACodeFarFromNow()
    {
        var (user, secret) = await UserWithTotp();

        Assert.False(await _bed.Verification.VerifyTotp(user.Id, _bed.CodeFor(secret, TimeSpan.FromMinutes(5))));
    }

    [Fact]
    public async Task VerifyTotp_RejectsAWrongCode()
    {
        var (user, _) = await UserWithTotp();

        Assert.False(await _bed.Verification.VerifyTotp(user.Id, "000000"));
    }

    [Fact]
    public async Task VerifyTotp_RejectsAnUnconfirmedSecret()
    {
        var user = await _bed.SeedUser();
        await _bed.Enrollment.BeginTotp(user);

        Assert.False(await _bed.Verification.VerifyTotp(user.Id, _bed.CodeFor(await _bed.PendingSecret(user.Id))));
    }

    [Fact]
    public async Task VerifyTotp_RejectsUsersWithoutAnAuthenticatorApp()
    {
        var user = await _bed.SeedUser();

        Assert.False(await _bed.Verification.VerifyTotp(user.Id, "123456"));
    }

    [Fact]
    public async Task VerifyTotp_RejectsTheCodeOfAnotherUser()
    {
        var (_, secret) = await UserWithTotp();
        var other = await _bed.SeedUser("other@test.com");

        Assert.False(await _bed.Verification.VerifyTotp(other.Id, _bed.CodeFor(secret)));
    }

    [Fact]
    public async Task VerifyRecoveryCode_UsesUpTheCode()
    {
        var (user, _) = await UserWithTotp();
        var code = (await _bed.RecoveryCodes.Issue(user.Id))[0];

        Assert.NotEqual(None, await _bed.Verification.VerifyRecoveryCode(user.Id, code));
        Assert.Equal(None, await _bed.Verification.VerifyRecoveryCode(user.Id, code));
    }

    [Fact]
    public async Task BeginPasskey_WithoutPasskeys_OffersNothing()
    {
        var user = await _bed.SeedUser();

        Assert.Equal(None, await _bed.Verification.BeginPasskey(TwoFactorTestBed.Origin, user.Id));
    }

    [Fact]
    public async Task VerifyPasskey_AcceptsTheRegisteredAuthenticatorAndStoresTheCounter()
    {
        var user = await _bed.SeedUser();
        using var authenticator = await _bed.EnablePasskey(user);

        var options = (await _bed.Verification.BeginPasskey(TwoFactorTestBed.Origin, user.Id))
            .Match(() => throw new InvalidOperationException(), found => found);

        Assert.Single(options.AllowCredentials);
        Assert.True(await _bed.Verification.VerifyPasskey(
            TwoFactorTestBed.Origin,
            user.Id,
            options,
            authenticator.Assert(options, TwoFactorTestBed.OriginText)));
        Assert.Equal(1, (await _bed.Queries.GetPasskeys(user.Id)).Single().SignCount);
    }

    [Fact]
    public async Task VerifyPasskey_RejectsAReplayOfTheSameAssertion()
    {
        var user = await _bed.SeedUser();
        using var authenticator = await _bed.EnablePasskey(user);
        var options = (await _bed.Verification.BeginPasskey(TwoFactorTestBed.Origin, user.Id))
            .Match(() => throw new InvalidOperationException(), found => found);
        var assertion = authenticator.Assert(options, TwoFactorTestBed.OriginText);

        Assert.True(await _bed.Verification.VerifyPasskey(TwoFactorTestBed.Origin, user.Id, options, assertion));
        Assert.False(await _bed.Verification.VerifyPasskey(TwoFactorTestBed.Origin, user.Id, options, assertion));
    }

    [Fact]
    public async Task VerifyPasskey_RejectsAnAuthenticatorOfAnotherUser()
    {
        var user = await _bed.SeedUser();
        var other = await _bed.SeedUser("other@test.com");
        using var authenticator = await _bed.EnablePasskey(user);
        using var otherAuthenticator = await _bed.EnablePasskey(other);
        var options = (await _bed.Verification.BeginPasskey(TwoFactorTestBed.Origin, user.Id))
            .Match(() => throw new InvalidOperationException(), found => found);

        Assert.False(await _bed.Verification.VerifyPasskey(
            TwoFactorTestBed.Origin,
            user.Id,
            options,
            otherAuthenticator.Assert(options, TwoFactorTestBed.OriginText)));
    }

    [Fact]
    public async Task VerifyPasskey_RejectsAnotherOrigin()
    {
        var user = await _bed.SeedUser();
        using var authenticator = await _bed.EnablePasskey(user);
        var options = (await _bed.Verification.BeginPasskey(TwoFactorTestBed.Origin, user.Id))
            .Match(() => throw new InvalidOperationException(), found => found);

        Assert.False(await _bed.Verification.VerifyPasskey(
            TwoFactorTestBed.Origin,
            user.Id,
            options,
            authenticator.Assert(options, "https://evil.test")));
    }
}
