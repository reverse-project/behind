using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace Behind.Tests.CoreWebApi.Identity;

public class JwtSettingsExtensionsTests
{
    private static readonly JwtSettings Settings = new("issuer", "audience", new string('k', 64));

    private static string TokenFor(
        JwtSettings settings,
        Guid sessionId,
        Guid nonce,
        DateTime expires,
        DateTime? notBefore = null) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim("session_id", sessionId.ToString()),
                new Claim("token_nonce", nonce.ToString())
            ]),
            NotBefore = notBefore ?? expires.AddHours(-1),
            Expires = expires,
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                SecurityAlgorithms.HmacSha256Signature)
        });

    [Fact]
    public async Task ReadSessionClaims_ValidToken_ReturnsSessionAndNonce()
    {
        var sessionId = Guid.NewGuid();
        var nonce = Guid.NewGuid();

        var claims = await Settings.ReadSessionClaims(TokenFor(Settings, sessionId, nonce, DateTime.UtcNow.AddMinutes(5)));

        var found = claims.Match(() => throw new Xunit.Sdk.XunitException("None"), c => c);
        Assert.Equal(sessionId, found.SessionId);
        Assert.Equal<Guid>(nonce, found.Nonce);
    }

    [Fact]
    public async Task ReadSessionClaims_ExpiredToken_ReturnsNone()
    {
        var token = TokenFor(Settings, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddSeconds(-1));

        Assert.Equal(None, await Settings.ReadSessionClaims(token));
    }

    [Fact]
    public async Task ReadSessionClaims_WrongSigningKey_ReturnsNone()
    {
        var token = TokenFor(
            Settings with { SigningKey = new string('x', 64) },
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow.AddMinutes(5));

        Assert.Equal(None, await Settings.ReadSessionClaims(token));
    }

    [Fact]
    public async Task ReadSessionClaims_WrongAudience_ReturnsNone()
    {
        var token = TokenFor(
            Settings with { Audience = "other" },
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow.AddMinutes(5));

        Assert.Equal(None, await Settings.ReadSessionClaims(token));
    }

    [Fact]
    public async Task ReadSessionClaims_Garbage_ReturnsNone() =>
        Assert.Equal(None, await Settings.ReadSessionClaims("not-a-token"));

    [Fact]
    public void ValidationParameters_DoNotTolerateClockSkew() =>
        Assert.Equal(TimeSpan.Zero, Settings.ValidationParameters.ClockSkew);
}
