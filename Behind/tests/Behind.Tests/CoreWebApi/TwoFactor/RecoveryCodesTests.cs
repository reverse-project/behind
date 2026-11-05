using Behind.Core.Common;
using Behind.Core.WebApi.TwoFactor;

namespace Behind.Tests.CoreWebApi.TwoFactor;

public sealed class RecoveryCodesTests : IDisposable
{
    private readonly TwoFactorTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private static RecoveryOutcome Used(Option<RecoveryOutcome> outcome) =>
        outcome.Match(() => throw new InvalidOperationException(), found => found);

    [Fact]
    public async Task Issue_StoresTwentyHashedCodesAndReturnsThePlainOnes()
    {
        var user = await _bed.SeedUser();

        var codes = await _bed.RecoveryCodes.Issue(user.Id);
        var stored = await _bed.Queries.GetRecoveryCodes(user.Id);

        Assert.Equal(20, codes.Count);
        Assert.Equal(20, stored.Count);
        Assert.All(codes, code => Assert.Equal(8, code.Length));
        Assert.DoesNotContain(stored, row => codes.Contains(row.Hash));
        Assert.All(codes, code => Assert.Contains(stored, row => code.MatchesRecoveryCodeHash(row.Hash)));
    }

    [Fact]
    public async Task Issue_ReplacesThePreviousCodes()
    {
        var user = await _bed.SeedUser();

        var first = await _bed.RecoveryCodes.Issue(user.Id);
        var second = await _bed.RecoveryCodes.Issue(user.Id);

        Assert.Equal(20, (await _bed.Queries.GetRecoveryCodes(user.Id)).Count);
        Assert.Equal(None, await _bed.RecoveryCodes.Redeem(user.Id, first[0]));
        Assert.NotEqual(None, await _bed.RecoveryCodes.Redeem(user.Id, second[0]));
    }

    [Fact]
    public async Task Redeem_ConsumesTheCodeSoItWorksOnlyOnce()
    {
        var user = await _bed.SeedUser();
        var codes = await _bed.RecoveryCodes.Issue(user.Id);

        var outcome = Used(await _bed.RecoveryCodes.Redeem(user.Id, codes[3]));

        Assert.Empty(outcome.NewCodes);
        Assert.Equal(19, (await _bed.Queries.GetRecoveryCodes(user.Id)).Count);
        Assert.Equal(None, await _bed.RecoveryCodes.Redeem(user.Id, codes[3]));
    }

    [Fact]
    public async Task Redeem_IgnoresCaseAndPadding()
    {
        var user = await _bed.SeedUser();
        var codes = await _bed.RecoveryCodes.Issue(user.Id);

        Assert.NotEqual(None, await _bed.RecoveryCodes.Redeem(user.Id, $"  {codes[0].ToLowerInvariant()} "));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("AAAAAAAA")]
    [InlineData("not a code!")]
    public async Task Redeem_RejectsWrongOrMalformedCodes(string code)
    {
        var user = await _bed.SeedUser();
        await _bed.RecoveryCodes.Issue(user.Id);

        Assert.Equal(None, await _bed.RecoveryCodes.Redeem(user.Id, code));
        Assert.Equal(20, (await _bed.Queries.GetRecoveryCodes(user.Id)).Count);
    }

    [Fact]
    public async Task Redeem_RejectsTheCodesOfAnotherUser()
    {
        var user = await _bed.SeedUser();
        var other = await _bed.SeedUser("other@test.com");
        var codes = await _bed.RecoveryCodes.Issue(user.Id);

        Assert.Equal(None, await _bed.RecoveryCodes.Redeem(other.Id, codes[0]));
        Assert.Equal(20, (await _bed.Queries.GetRecoveryCodes(user.Id)).Count);
    }

    [Fact]
    public async Task Redeem_TheLastCodeIssuesAFreshBunch()
    {
        var user = await _bed.SeedUser();
        var codes = await _bed.RecoveryCodes.Issue(user.Id);

        foreach (var code in codes.Take(19))
        {
            Assert.Empty(Used(await _bed.RecoveryCodes.Redeem(user.Id, code)).NewCodes);
        }

        var outcome = Used(await _bed.RecoveryCodes.Redeem(user.Id, codes[19]));

        Assert.Equal(20, outcome.NewCodes.Count);
        Assert.Empty(outcome.NewCodes.Intersect(codes));
        Assert.Equal(20, (await _bed.Queries.GetRecoveryCodes(user.Id)).Count);
        Assert.NotEqual(None, await _bed.RecoveryCodes.Redeem(user.Id, outcome.NewCodes[0]));
    }
}
