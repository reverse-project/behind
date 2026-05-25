using System.ComponentModel.DataAnnotations;
using static Behind.Common.FunctionalExtensions;
using Behind.Core.Common.Configuration;
using Behind.Core.Domain;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public sealed class SignInEndpoints
{
    public sealed record PasswordRequest(
        [Required, DataType(DataType.EmailAddress)]
        string Email,
        [Required] string Password);

    public sealed record SignInResponse(string AccessToken, DateTimeOffset Expires, string RefreshToken);

    public static async Task<IResult> SignInPassword(UserManager<ApplicationUser> userManager,
        IOptions<SecurityConfiguration> securityConfiguration, PasswordRequest request)
    {
        if (securityConfiguration.Value.Jwt is null)
        {
            return Results.InternalServerError();
        }

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || user.Password?.Verify(request.Password) != true)
        {
            return Results.Unauthorized();
        }

        var accessToken = AccessToken.Create(new HumanUser([new Email(user.NormalizedUserName)], None));
        return Results.Json(new SignInResponse(
            accessToken.ToJwtToken(user, securityConfiguration.Value.Jwt),
            accessToken.ExpiresAt,
            RefreshToken
                .Create(new HumanUser([new Email(user.NormalizedUserName)], None))
                .ToJwtToken(user, securityConfiguration.Value.Jwt)
        ));
    }
}