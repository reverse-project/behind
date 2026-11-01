using System.ComponentModel.DataAnnotations;
using Behind.Core.WebApi.Endpoints;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class RequestValidationTests
{
    private static bool IsValid(object request) =>
        Validator.TryValidateObject(request, new ValidationContext(request), [], true);

    [Fact]
    public void SignInPasswordRequest_RequiresEmailAndPassword()
    {
        Assert.True(IsValid(new SignInEndpoints.PasswordRequest("user@test.com", "secret")));
        Assert.False(IsValid(new SignInEndpoints.PasswordRequest("", "secret")));
        Assert.False(IsValid(new SignInEndpoints.PasswordRequest("user@test.com", "")));
    }

    [Fact]
    public void SignInMagicLinkRequests_RequireTheirValues()
    {
        Assert.True(IsValid(new SignInEndpoints.MagicLinkRequest("user@test.com")));
        Assert.False(IsValid(new SignInEndpoints.MagicLinkRequest("")));
        Assert.True(IsValid(new SignInEndpoints.MagicLinkSignInRequest("token")));
        Assert.False(IsValid(new SignInEndpoints.MagicLinkSignInRequest("")));
    }

    [Fact]
    public void SignUpPasswordRequest_RequiresCredentialsAndLimitsTheDisplayName()
    {
        Assert.True(IsValid(new SignUpEndpoints.PasswordRequest("user@test.com", "secret", "Ada")));
        Assert.False(IsValid(new SignUpEndpoints.PasswordRequest("", "secret")));
        Assert.False(IsValid(new SignUpEndpoints.PasswordRequest("user@test.com", "")));
        Assert.False(IsValid(new SignUpEndpoints.PasswordRequest("user@test.com", "secret", new string('x', 501))));
    }

    [Theory]
    [InlineData("123456", true)]
    [InlineData("12345678", true)]
    [InlineData("12345", false)]
    [InlineData("123456789", false)]
    [InlineData("12345a", false)]
    [InlineData("", false)]
    public void ElevationRequest_RequiresASixToEightDigitCode(string code, bool valid)
    {
        Assert.Equal(valid, IsValid(new ElevationEndpoints.ElevationRequest(code)));
    }

    [Fact]
    public void DisplayNameRequest_LimitsTheLength()
    {
        Assert.True(IsValid(new ProfileEndpoints.DisplayNameRequest(null)));
        Assert.True(IsValid(new ProfileEndpoints.DisplayNameRequest(new string('x', 500))));
        Assert.False(IsValid(new ProfileEndpoints.DisplayNameRequest(new string('x', 501))));
    }
}
