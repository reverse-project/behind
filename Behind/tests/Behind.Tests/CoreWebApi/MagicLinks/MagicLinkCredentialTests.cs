using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.MagicLinks;

namespace Behind.Tests.CoreWebApi.MagicLinks;

public class MagicLinkCredentialTests
{
    private const string Pepper = "server-side-pepper";

    private static readonly Uuid Actual = Uuid.CreateVersion7(DateTimeOffset.UtcNow);
    private static readonly Uuid Old = Uuid.CreateVersion7(DateTimeOffset.UtcNow.AddDays(-5));

    [Fact]
    public void Create_ProducesCredentialSignedBySecret()
    {
        var nonce = new TokenNonce(Guid.NewGuid());

        var credential = MagicLinkCredential.Create(nonce, Actual, Pepper);

        Assert.Equal(nonce, credential.Nonce);
        Assert.True(credential.IsSignedBy([Actual], Pepper));
    }

    [Fact]
    public void Create_UsesDifferentRandomValuePerCredential()
    {
        var nonce = new TokenNonce(Guid.NewGuid());

        Assert.NotEqual(
            MagicLinkCredential.Create(nonce, Actual, Pepper).Random,
            MagicLinkCredential.Create(nonce, Actual, Pepper).Random);
    }

    [Fact]
    public void Token_RoundTripsThroughTryParse()
    {
        var credential = MagicLinkCredential.Create(Guid.NewGuid(), Actual, Pepper);

        var parsed = MagicLinkCredential.TryParse(credential.Token);

        Assert.Equal(Some(credential), parsed);
    }

    [Fact]
    public void Token_IsUrlSafe() =>
        Assert.Matches(@"^[0-9a-f]{32}\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$",
            MagicLinkCredential.Create(Guid.NewGuid(), Actual, Pepper).Token);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("only")]
    [InlineData("a.b")]
    [InlineData("a.b.c.d")]
    [InlineData("not-a-guid.random.hash")]
    public void TryParse_Malformed_ReturnsNone(string? token) =>
        Assert.Equal(None, MagicLinkCredential.TryParse(token));

    [Fact]
    public void IsSignedBy_OldSecretAmongValidSecrets_IsAccepted()
    {
        var credential = MagicLinkCredential.Create(Guid.NewGuid(), Old, Pepper);

        Assert.True(credential.IsSignedBy([Actual, Old], Pepper));
    }

    [Fact]
    public void IsSignedBy_UnknownSecret_IsRejected()
    {
        var credential = MagicLinkCredential.Create(Guid.NewGuid(), Old, Pepper);

        Assert.False(credential.IsSignedBy([Actual], Pepper));
        Assert.False(credential.IsSignedBy([], Pepper));
    }

    [Fact]
    public void IsSignedBy_WrongPepper_IsRejected()
    {
        var credential = MagicLinkCredential.Create(Guid.NewGuid(), Actual, Pepper);

        Assert.False(credential.IsSignedBy([Actual], "another-pepper"));
    }

    [Fact]
    public void IsSignedBy_TamperedNonce_IsRejected()
    {
        var credential = MagicLinkCredential.Create(Guid.NewGuid(), Actual, Pepper);

        Assert.False((credential with { Nonce = Guid.NewGuid() }).IsSignedBy([Actual], Pepper));
    }

    [Fact]
    public void IsSignedBy_TamperedRandom_IsRejected()
    {
        var credential = MagicLinkCredential.Create(Guid.NewGuid(), Actual, Pepper);

        Assert.False((credential with { Random = "forged" }).IsSignedBy([Actual], Pepper));
    }

    [Fact]
    public void ToLink_AppendsEscapedTokenToFrontendHashRoute()
    {
        var credential = MagicLinkCredential.Create(Guid.NewGuid(), Actual, Pepper);

        var link = credential.ToLink(new Uri("https://example.com/app/"));

        Assert.Equal(
            $"https://example.com/app/#{MagicLinkExtensions.SignInRoute}?{MagicLinkExtensions.TokenParameter}={Uri.EscapeDataString(credential.Token)}",
            link);
    }

    [Fact]
    public void ToLink_BaseUriWithQueryAndFragment_DropsThemSoTokenStaysInHash()
    {
        var credential = MagicLinkCredential.Create(Guid.NewGuid(), Actual, Pepper);

        var link = credential.ToLink(new Uri("https://example.com/app?x=1#old"));

        Assert.StartsWith($"https://example.com/app#{MagicLinkExtensions.SignInRoute}?", link);
        Assert.DoesNotContain("x=1", link);
        Assert.Equal(1, link.Count(character => character == '#'));
    }
}
