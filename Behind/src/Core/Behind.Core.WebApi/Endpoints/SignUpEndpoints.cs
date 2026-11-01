using System.ComponentModel.DataAnnotations;
using Behind.Common.Logging;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Domain;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi.Endpoints;

public sealed class SignUpEndpoints
{
    public sealed record PasswordRequest([property: Required, DataType(DataType.EmailAddress)] string Email,
        [property: Required] string Password,
        [property: MaxLength(DisplayNameExtensions.MaxLength)] string? DisplayName = null);

    [IgnoreETag]
    public static async Task<IResult> SignUpPassword(
        UserManager<ApplicationUser> userManager,
        ILogger<SignUpEndpoints> logger,
        PasswordRequest request)
    {
        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            logger.LogWarning("Sign-up rejected: the email is already in use");

            return Results.Conflict("Email already in use");
        }

        var user = new ApplicationUser
        {
            DisplayName = request.DisplayName.ToDisplayName(),
            Emails = [new Email(request.Email)]
        };
        
        var result = await userManager.CreateAsync(user, request.Password);

        if (result.Succeeded)
        {
            logger.LogInformation("User {UserId} signed up with a password", () => [user.Id]);

            return Results.StatusCode(204);
        }

        logger.LogWarning(
            "Sign-up of user {UserId} failed with {ErrorCodes}",
            () => [user.Id, result.Errors.Select(e => e.Code).ToArray()]);

        return Results.BadRequest(result.Errors.Select(e => e.Description));
    }
}