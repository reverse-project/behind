namespace Behind.Tests.Domain;

public class MagicLinkTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_SetsExpiryFromLifetimeAndGeneratesNonce()
    {
        var first = MagicLink.Create(Now, TimeSpan.FromMinutes(15));
        var second = MagicLink.Create(Now, TimeSpan.FromMinutes(15));

        Assert.Equal(Now, first.IssuedAt);
        Assert.Equal(Now.AddMinutes(15), first.ExpiresAt);
        Assert.Equal(None, first.ConsumedAt);
        Assert.NotEqual(first.Nonce, second.Nonce);
    }

    [Fact]
    public void Create_DefaultsToTheSignInFunction() =>
        Assert.Equal(MagicLinkFunction.SignIn, MagicLink.Create(Now, TimeSpan.FromMinutes(15)).Function);

    [Fact]
    public void Create_KeepsTheRequestedFunction() =>
        Assert.Equal(
            MagicLinkFunction.LostPassword,
            MagicLink.Create(Now, TimeSpan.FromMinutes(15), MagicLinkFunction.LostPassword).Function);

    [Fact]
    public void IsUsableAt_BeforeExpiry_IsTrue() =>
        Assert.True(MagicLink.Create(Now, TimeSpan.FromMinutes(15)).IsUsableAt(Now.AddMinutes(14)));

    [Fact]
    public void IsUsableAt_AtOrAfterExpiry_IsFalse()
    {
        var link = MagicLink.Create(Now, TimeSpan.FromMinutes(15));

        Assert.False(link.IsUsableAt(Now.AddMinutes(15)));
        Assert.False(link.IsUsableAt(Now.AddHours(1)));
    }

    [Fact]
    public void IsUsableAt_Consumed_IsFalse() =>
        Assert.False((MagicLink.Create(Now, TimeSpan.FromMinutes(15)) with { ConsumedAt = Some(Now) }).IsUsableAt(Now));
}
